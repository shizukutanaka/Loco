"""
Loco Workflow Automation Python SDK
Enterprise-grade async workflow automation client library

Features:
- Async/await support via httpx
- Full type hints with TypedDict
- Automatic JWT token management
- Structured error handling
- Built-in retry logic with exponential backoff
- Request correlation tracking

Example:
    async with LocoClient("https://api.loco.io", username="u", password="p") as client:
        workflows = await client.workflows.list()
        execution = await client.workflows.execute("workflow-1", params={})
"""

from __future__ import annotations

import asyncio
import logging
import uuid
from datetime import datetime, timedelta
from typing import Any, AsyncContextManager, Dict, List, Optional, TypedDict

try:
    import httpx
except ImportError:
    raise ImportError("httpx is required. Install with: pip install httpx")

try:
    import jwt
except ImportError:
    raise ImportError("pyjwt is required. Install with: pip install pyjwt")


# The statuses an execution can end on. ExecutionResponseFactory.
# ToFrontendStatus is the only thing that produces them, and it emits
# lowercase - so anything comparing against "Completed" waits forever.
TERMINAL_STATUSES = frozenset({"completed", "failed", "cancelled"})


# Type definitions.
#
# Field names are camelCase because the API serializes that way
# (JsonNamingPolicy.CamelCase, set in Program.cs). These were snake_case, which
# is idiomatic Python and simply wrong here: nothing renames the keys on the
# way in, so `workflow["created_at"]` was a KeyError on every response.
class WorkflowDict(TypedDict, total=False):
    """A stored workflow, as the API returns it."""
    id: str
    name: str
    description: Optional[str]
    nodes: List[Dict[str, Any]]
    edges: List[Dict[str, Any]]
    metadata: Dict[str, Any]
    createdAt: str
    updatedAt: str


class ExecutionResultDict(TypedDict, total=False):
    """
    One execution, as the API returns it.

    `output` and `logs` appear once the run finishes; `error` only on a failed
    or cancelled one.
    """
    executionId: str
    status: str
    startedAt: str
    completedAt: Optional[str]
    output: Optional[Dict[str, Any]]
    error: Optional[Dict[str, Any]]
    logs: Optional[List[Dict[str, Any]]]


class TokenResponse(TypedDict):
    """The token endpoint's payload."""
    accessToken: str
    tokenType: str
    expiresIn: int
    scope: str


class LocoException(Exception):
    """Base exception for Loco SDK"""
    pass


class LocoAuthError(LocoException):
    """Authentication error"""
    pass


class LocoNotFoundError(LocoException):
    """Resource not found error"""
    pass


class LocoValidationError(LocoException):
    """Validation error"""
    pass


class LocoServerError(LocoException):
    """Server error"""
    pass


class RateLimitError(LocoException):
    """Rate limit exceeded error"""
    pass


logger = logging.getLogger(__name__)


class LocoClient:
    """
    Async Loco Workflow Automation API client

    Two ways to authenticate, both producing the bearer token the API expects:
    - username and password: a token is fetched, and refreshed before expiry
    - jwt_token: a token you already hold
    """

    def __init__(
        self,
        base_url: str,
        username: Optional[str] = None,
        password: Optional[str] = None,
        jwt_token: Optional[str] = None,
        timeout: float = 30.0,
        max_retries: int = 3,
        verify_ssl: bool = True,
    ):
        """
        Initialize Loco client

        Args:
            base_url: API base URL (e.g., "https://api.loco.io")
            username: Username for token-based auth
            password: Password for token-based auth
            jwt_token: Pre-generated JWT token
            timeout: Request timeout in seconds
            max_retries: Maximum number of retries
            verify_ssl: Whether to verify SSL certificates
        """
        self.base_url = base_url.rstrip("/")
        self.username = username
        self.password = password
        self.jwt_token = jwt_token
        self.timeout = timeout
        self.max_retries = max_retries
        self.verify_ssl = verify_ssl

        self._client: Optional[httpx.AsyncClient] = None
        self._token_expiry: Optional[datetime] = None
        self._correlation_id = str(uuid.uuid4())

    async def __aenter__(self) -> LocoClient:
        """Async context manager entry"""
        self._client = httpx.AsyncClient(
            base_url=self.base_url,
            timeout=self.timeout,
            verify=self.verify_ssl,
            follow_redirects=True,
        )
        return self

    async def __aexit__(self, exc_type: Any, exc_val: Any, exc_tb: Any) -> None:
        """Async context manager exit"""
        if self._client:
            await self._client.aclose()

    async def authenticate(self) -> TokenResponse:
        """
        Authenticate with username and password

        Returns:
            {"accessToken": ..., "tokenType": "Bearer", "expiresIn": ..., "scope": ...}

        Raises:
            LocoAuthError: If authentication fails
        """
        if not self.username or not self.password:
            raise LocoAuthError("Username and password are required")

        try:
            response = await self._request(
                "POST",
                "/api/v1/authentication/token",
                json={"username": self.username, "password": self.password},
                skip_auth=True,
            )
            # camelCase: the API serializes with JsonNamingPolicy.CamelCase
            # (Program.cs). Reading access_token/expires_in here meant the
            # client could not authenticate at all, so every other method was
            # unreachable.
            self.jwt_token = response["accessToken"]
            self._token_expiry = datetime.utcnow() + timedelta(
                seconds=response["expiresIn"]
            )
            logger.info("Authentication successful, token expires at %s", self._token_expiry)
            return response
        except Exception as e:
            raise LocoAuthError(f"Authentication failed: {str(e)}")

    async def _ensure_authenticated(self) -> None:
        """Ensure client is authenticated"""
        if not self.jwt_token:
            if self.username and self.password:
                await self.authenticate()
            else:
                raise LocoAuthError("No authentication method provided")

        # Refresh token if expired
        if self.jwt_token and self._token_expiry:
            if datetime.utcnow() > self._token_expiry - timedelta(minutes=5):
                logger.info("Token expiring soon, refreshing...")
                await self.authenticate()

    async def _request(
        self,
        method: str,
        endpoint: str,
        json: Optional[Dict[str, Any]] = None,
        params: Optional[Dict[str, Any]] = None,
        skip_auth: bool = False,
        **kwargs: Any,
    ) -> Dict[str, Any]:
        """
        Make authenticated HTTP request with retry logic

        Args:
            method: HTTP method (GET, POST, etc.)
            endpoint: API endpoint path
            json: JSON request body
            params: Query parameters
            skip_auth: Skip authentication
            **kwargs: Additional httpx parameters

        Returns:
            Response JSON

        Raises:
            Various LocoException subclasses
        """
        if not self._client:
            raise LocoException("Client not initialized. Use 'async with' context manager.")

        if not skip_auth:
            await self._ensure_authenticated()

        headers = {
            "X-Correlation-ID": self._correlation_id,
            "User-Agent": "loco-python-sdk/1.0.0",
        }

        # Bearer only. The API registers exactly one authentication scheme,
        # JwtBearer (Program.cs), and reads no X-Api-Key header - so the
        # api_key option this client used to offer sent something the server
        # ignored, and every call came back 401.
        if self.jwt_token:
            headers["Authorization"] = f"Bearer {self.jwt_token}"

        # Retry logic with exponential backoff
        last_exception: Optional[Exception] = None
        for attempt in range(self.max_retries):
            try:
                response = await self._client.request(
                    method,
                    endpoint,
                    json=json,
                    params=params,
                    headers=headers,
                    **kwargs,
                )

                # Handle different status codes
                if response.status_code == 401:
                    raise LocoAuthError("Unauthorized")
                elif response.status_code == 403:
                    raise LocoAuthError("Forbidden")
                elif response.status_code == 404:
                    raise LocoNotFoundError("Resource not found")
                elif response.status_code == 429:
                    raise RateLimitError("Rate limit exceeded")
                elif response.status_code >= 500:
                    raise LocoServerError(f"Server error: {response.status_code}")
                elif response.status_code >= 400:
                    raise LocoValidationError(f"Bad request: {response.status_code}")

                response.raise_for_status()
                return self._unwrap(response.json())

            except (httpx.TimeoutException, httpx.ConnectError) as e:
                last_exception = e
                wait_time = 2 ** attempt
                if attempt < self.max_retries - 1:
                    logger.warning(
                        "Request failed (attempt %d/%d), retrying in %ds: %s",
                        attempt + 1,
                        self.max_retries,
                        wait_time,
                        str(e),
                    )
                    await asyncio.sleep(wait_time)
                continue

        raise last_exception or LocoException("Request failed after retries")

    @staticmethod
    def _unwrap(body: Any) -> Dict[str, Any]:
        """
        Return the payload from Loco's response envelope.

        Every endpoint answers with the same shape (Loco.Api.Contracts.
        ApiEnvelope):

            {"success": true,  "data": {...},  "message": "..."}
            {"success": false, "error": {"code": "...", "message": "..."}}

        This client used to hand the whole envelope back to the caller, so
        `result["status"]` was a KeyError on a response that had actually
        succeeded, and a failure arrived as the bare HTTP code with the
        server's explanation thrown away.

        `/health` is not enveloped - it is ASP.NET Core's health endpoint - so
        a body without a `success` key is passed through unchanged.
        """
        if not isinstance(body, dict) or "success" not in body:
            return body

        if body.get("success"):
            data = body.get("data")
            return data if isinstance(data, dict) else {}

        error = body.get("error") or {}
        code = error.get("code", "UNKNOWN")
        message = error.get("message", "Request failed")

        if code in ("UNAUTHORIZED", "AUTH_NOT_CONFIGURED"):
            raise LocoAuthError(f"{code}: {message}")
        if code == "NOT_FOUND":
            raise LocoNotFoundError(f"{code}: {message}")
        if code in ("INVALID_ARGUMENT", "INVALID_WORKFLOW", "UNKNOWN_CONNECTOR"):
            raise LocoValidationError(f"{code}: {message}")

        raise LocoException(f"{code}: {message}")

    # Workflow operations
    async def list_workflows(
        self, page: int = 1, page_size: int = 20
    ) -> Dict[str, Any]:
        """
        List workflows.

        Args:
            page: 1-based page number
            page_size: Items per page (max 100)

        Returns:
            {"workflows": [...], "total": int, "page": int, "pageSize": int}

        The API pages by page/pageSize (WorkflowsController.GetWorkflows).
        This client sent skip/take, which ASP.NET Core simply ignored - so
        every call returned the first page whatever was asked for, with no
        error to say so.
        """
        return await self._request(
            "GET",
            "/api/v1/workflows",
            params={"page": page, "pageSize": min(page_size, 100)},
        )

    async def get_workflow(self, workflow_id: str) -> WorkflowDict:
        """Get workflow by ID"""
        return await self._request("GET", f"/api/v1/workflows/{workflow_id}")

    async def create_workflow(
        self,
        name: str,
        description: Optional[str] = None,
        nodes: Optional[List[Dict[str, Any]]] = None,
        edges: Optional[List[Dict[str, Any]]] = None,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> WorkflowDict:
        """
        Create a workflow.

        A Loco workflow is a node graph, not a step list: `nodes` carry the
        actions and `edges` connect them, exactly as the visual editor saves
        them. This client used to send `steps`, a field
        WorkflowCreateRequest has no property for, so every workflow it
        created came back empty - accepted, stored, and containing nothing.
        """
        return await self._request(
            "POST",
            "/api/v1/workflows",
            json={
                "name": name,
                "description": description,
                "nodes": nodes or [],
                "edges": edges or [],
                "metadata": metadata or {},
            },
        )

    async def update_workflow(
        self,
        workflow_id: str,
        name: Optional[str] = None,
        description: Optional[str] = None,
        nodes: Optional[List[Dict[str, Any]]] = None,
        edges: Optional[List[Dict[str, Any]]] = None,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> WorkflowDict:
        """Update a workflow. Only the fields supplied are changed."""
        payload: Dict[str, Any] = {}
        if name is not None:
            payload["name"] = name
        if description is not None:
            payload["description"] = description
        if nodes is not None:
            payload["nodes"] = nodes
        if edges is not None:
            payload["edges"] = edges
        if metadata is not None:
            payload["metadata"] = metadata

        return await self._request(
            "PUT", f"/api/v1/workflows/{workflow_id}", json=payload
        )

    async def delete_workflow(self, workflow_id: str) -> None:
        """Delete workflow"""
        await self._request("DELETE", f"/api/v1/workflows/{workflow_id}")

    async def execute_workflow(
        self,
        workflow_id: str,
        input: Optional[Dict[str, Any]] = None,
        dry_run: bool = False,
    ) -> ExecutionResultDict:
        """
        Start a workflow run.

        Args:
            workflow_id: Workflow ID
            input: Initial variables, available to the workflow's nodes
            dry_run: Validate and plan without invoking any connector

        Returns:
            The execution as first reported: at least executionId and status.
            Execution is always asynchronous - the call returns as soon as the
            run is registered. Use wait_for_execution to follow it.

        The body is {"input": ..., "dryRun": ...} (ExecuteRequest). This
        client sent {"parameters": ..., "asyncExecution": ...}, so initial
        variables never reached the workflow and dry runs silently executed
        for real.
        """
        return await self._request(
            "POST",
            f"/api/v1/workflows/{workflow_id}/execute",
            json={"input": input or {}, "dryRun": dry_run},
        )

    async def get_execution_status(self, execution_id: str) -> Dict[str, Any]:
        """
        Get one execution.

        Executions are addressed globally by id - GET /api/v1/executions/{id}
        (ExecutionsController) - not nested under their workflow. This client
        asked for /api/v1/workflows/{workflow_id}/executions/{id}, a route
        that does not exist, so it 404'd on every call.
        """
        return await self._request("GET", f"/api/v1/executions/{execution_id}")

    async def cancel_execution(self, execution_id: str) -> Dict[str, Any]:
        """Ask a running execution to stop."""
        return await self._request(
            "POST", f"/api/v1/executions/{execution_id}/cancel"
        )

    # Convenience methods
    async def wait_for_execution(
        self,
        execution_id: str,
        timeout: float = 300.0,
        poll_interval: float = 1.0,
    ) -> ExecutionResultDict:
        """
        Poll an execution until it finishes.

        Args:
            execution_id: Execution ID
            timeout: Maximum time to wait in seconds
            poll_interval: Time between polls in seconds

        Returns:
            Final execution result

        Raises:
            asyncio.TimeoutError: If timeout exceeded
        """
        start_time = datetime.utcnow()
        while True:
            status = await self.get_execution_status(execution_id)

            # Lowercase: ExecutionResponseFactory.ToFrontendStatus emits
            # pending/running/completed/failed/cancelled. Comparing against
            # "Completed" never matched, so this loop ran to timeout on runs
            # that had already succeeded.
            if status.get("status") in TERMINAL_STATUSES:
                return status

            elapsed = (datetime.utcnow() - start_time).total_seconds()
            if elapsed > timeout:
                raise asyncio.TimeoutError(
                    f"Execution {execution_id} did not complete within {timeout}s"
                )

            await asyncio.sleep(poll_interval)

    async def health_check(self) -> Dict[str, Any]:
        """Check API health"""
        return await self._request("GET", "/health", skip_auth=True)


# Convenience function for synchronous code
def create_client(
    base_url: str,
    username: Optional[str] = None,
    password: Optional[str] = None,
    jwt_token: Optional[str] = None,
) -> LocoClient:
    """
    Create a Loco client (for use with asyncio.run)

    Example:
        client = create_client("https://api.loco.io", username="u", password="p")
        async def main():
            async with client:
                workflows = await client.list_workflows()

        asyncio.run(main())
    """
    return LocoClient(
        base_url=base_url,
        username=username,
        password=password,
        jwt_token=jwt_token,
    )
