"""
Loco Python SDK Setup
Enterprise-grade workflow automation client library
"""

from setuptools import setup, find_packages

with open("README.md", "r", encoding="utf-8") as f:
    long_description = f.read()

setup(
    name="loco-client",
    version="1.0.0",
    author="Loco Team",
    author_email="support@loco.local",
    description="Enterprise-grade async workflow automation client for Loco",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/loco-automation/python-sdk",
    packages=find_packages(),
    python_requires=">=3.8",
    install_requires=[
        "httpx>=0.24.0",
        "pyjwt>=2.8.0",
    ],
    extras_require={
        "scheduler": ["apscheduler>=3.10.0"],
        "celery": ["celery>=5.3.0"],
        "dev": [
            "pytest>=7.4.0",
            "pytest-asyncio>=0.21.0",
            "pytest-cov>=4.1.0",
            "black>=23.0.0",
            "mypy>=1.5.0",
            "ruff>=0.1.0",
        ],
    },
    classifiers=[
        "Development Status :: 5 - Production/Stable",
        "Intended Audience :: Developers",
        "Topic :: Software Development :: Libraries :: Python Modules",
        "Topic :: Office/Business",
        "License :: OSI Approved :: MIT License",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
        "Programming Language :: Python :: 3.10",
        "Programming Language :: Python :: 3.11",
        "Programming Language :: Python :: 3.12",
        "Operating System :: OS Independent",
    ],
    keywords="workflow automation business-process orchestration scheduling",
    project_urls={
        "Bug Reports": "https://github.com/loco-automation/python-sdk/issues",
        "Source": "https://github.com/loco-automation/python-sdk",
        "Documentation": "https://docs.loco.io/sdk/python",
    },
)
