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
    async with LocoClient("https://api.loco.io", api_key="loco_xxx") as client:
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


# Type definitions
class WorkflowDict(TypedDict, total=False):
    """Workflow data structure"""
    id: str
    name: str
    description: Optional[str]
    steps: List[Dict[str, Any]]
    created_at: str
    updated_at: str


class ExecutionResultDict(TypedDict, total=False):
    """Execution result structure"""
    execution_id: str
    workflow_id: str
    status: str
    started_at: str
    completed_at: Optional[str]
    progress: int
    result: Optional[Dict[str, Any]]


class TokenResponse(TypedDict):
    """Token response structure"""
    access_token: str
    token_type: str
    expires_in: int
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

    Supports multiple authentication methods:
    - API Key: pass api_key parameter
    - Username/Password: call authenticate() first
    - JWT Token: pass jwt_token parameter
    """

    def __init__(
        self,
        base_url: str,
        api_key: Optional[str] = None,
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
            api_key: API key for authentication
            username: Username for token-based auth
            password: Password for token-based auth
            jwt_token: Pre-generated JWT token
            timeout: Request timeout in seconds
            max_retries: Maximum number of retries
            verify_ssl: Whether to verify SSL certificates
        """
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key
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
            Token response with access_token

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
            self.jwt_token = response["access_token"]
            self._token_expiry = datetime.utcnow() + timedelta(
                seconds=response["expires_in"]
            )
            logger.info("Authentication successful, token expires at %s", self._token_expiry)
            return response
        except Exception as e:
            raise LocoAuthError(f"Authentication failed: {str(e)}")

    async def _ensure_authenticated(self) -> None:
        """Ensure client is authenticated"""
        if not self.jwt_token and not self.api_key:
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

        if self.jwt_token:
            headers["Authorization"] = f"Bearer {self.jwt_token}"
        elif self.api_key:
            headers["X-Api-Key"] = self.api_key

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
                return response.json()

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

    # Workflow operations
    async def list_workflows(
        self, skip: int = 0, take: int = 20
    ) -> Dict[str, Any]:
        """
        List all workflows

        Args:
            skip: Number of items to skip
            take: Number of items to take (max 100)

        Returns:
            Paginated workflows response
        """
        return await self._request(
            "GET", "/api/v1/workflows", params={"skip": skip, "take": min(take, 100)}
        )

    async def get_workflow(self, workflow_id: str) -> WorkflowDict:
        """Get workflow by ID"""
        return await self._request("GET", f"/api/v1/workflows/{workflow_id}")

    async def create_workflow(
        self,
        name: str,
        description: Optional[str] = None,
        steps: Optional[List[Dict[str, Any]]] = None,
    ) -> WorkflowDict:
        """Create new workflow"""
        return await self._request(
            "POST",
            "/api/v1/workflows",
            json={"name": name, "description": description, "steps": steps},
        )

    async def update_workflow(
        self,
        workflow_id: str,
        name: Optional[str] = None,
        description: Optional[str] = None,
        steps: Optional[List[Dict[str, Any]]] = None,
    ) -> WorkflowDict:
        """Update existing workflow"""
        payload = {}
        if name is not None:
            payload["name"] = name
        if description is not None:
            payload["description"] = description
        if steps is not None:
            payload["steps"] = steps

        return await self._request(
            "PUT", f"/api/v1/workflows/{workflow_id}", json=payload
        )

    async def delete_workflow(self, workflow_id: str) -> None:
        """Delete workflow"""
        await self._request("DELETE", f"/api/v1/workflows/{workflow_id}")

    async def execute_workflow(
        self,
        workflow_id: str,
        parameters: Optional[Dict[str, Any]] = None,
        async_execution: bool = True,
    ) -> ExecutionResultDict:
        """
        Execute workflow

        Args:
            workflow_id: Workflow ID
            parameters: Workflow execution parameters
            async_execution: Whether to execute asynchronously

        Returns:
            Execution result
        """
        return await self._request(
            "POST",
            f"/api/v1/workflows/{workflow_id}/execute",
            json={"parameters": parameters or {}, "asyncExecution": async_execution},
        )

    async def get_execution_status(
        self, workflow_id: str, execution_id: str
    ) -> Dict[str, Any]:
        """Get workflow execution status"""
        return await self._request(
            "GET",
            f"/api/v1/workflows/{workflow_id}/executions/{execution_id}",
        )

    # Convenience methods
    async def wait_for_execution(
        self,
        workflow_id: str,
        execution_id: str,
        timeout: float = 300.0,
        poll_interval: float = 1.0,
    ) -> ExecutionResultDict:
        """
        Wait for workflow execution to complete

        Args:
            workflow_id: Workflow ID
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
            status = await self.get_execution_status(workflow_id, execution_id)

            if status["status"] in ("Completed", "Failed", "Cancelled"):
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
    api_key: Optional[str] = None,
    username: Optional[str] = None,
    password: Optional[str] = None,
    jwt_token: Optional[str] = None,
) -> LocoClient:
    """
    Create a Loco client (for use with asyncio.run)

    Example:
        client = create_client("https://api.loco.io", api_key="loco_xxx")
        async def main():
            async with client:
                workflows = await client.list_workflows()

        asyncio.run(main())
    """
    return LocoClient(
        base_url=base_url,
        api_key=api_key,
        username=username,
        password=password,
        jwt_token=jwt_token,
    )
