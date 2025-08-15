# Azure MCP Server Installation Guide

This guide helps AI agents and developers install and configure the Azure MCP Server for different environments.

## Installation Steps

### Configuration Setup

The Azure MCP Server requires configuration based on the client type. Below are the setup instructions for each supported client:

#### For VS Code Users

**✅ Recommended: Use the Azure MCP Server VS Code Extension**

1. Open VS Code and go to the Extensions view
   (`Ctrl+Shift+X` on Windows/Linux or `Cmd+Shift+X` on macOS).
2. Search for **"Azure MCP Server"** and install the official [Azure MCP Server extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azure-mcp-server) by Microsoft.
3. Open the Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`).
4. Run `MCP: List Servers`.
5. Select `azure-mcp-server-ext` from the list and click **Start** to launch the server.

**Alternative: Use the classic npx route via `.vscode/mcp.json`**

> **Requires Node.js (Latest LTS version)**

1. Create or modify the MCP configuration file, `mcp.json`, in your `.vscode` folder.

```json
{
  "servers": {
    "Azure MCP Server": {
      "command": "npx",
      "args": [
        "-y",
        "@azure/mcp@latest",
        "server",
        "start"
      ]
    }
  }
}
```

#### For Windsurf

> **Requires Node.js (Latest LTS version)**

1. Create or modify the configuration file at `~/.codeium/windsurf/mcp_config.json`:

```json
{
  "mcpServers": {
    "Azure MCP Server": {
      "command": "npx",
      "args": [
        "-y",
        "@azure/mcp@latest",
        "server",
        "start"
      ]
    }
  }
}
```
