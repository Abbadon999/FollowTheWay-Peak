#!/bin/bash

echo "========================================"
echo "FollowTheWay Build Script (Linux/macOS)"
echo "========================================"

# Check if API key is provided
if [ -z "$FOLLOWTHEWAY_API_KEY" ]; then
    echo "ERROR: FOLLOWTHEWAY_API_KEY environment variable is not set!"
    echo "Please set your API key: export FOLLOWTHEWAY_API_KEY=your_api_key_here"
    exit 1
fi

# Set default server URL if not provided
if [ -z "$FOLLOWTHEWAY_SERVER_URL" ]; then
    FOLLOWTHEWAY_SERVER_URL="https://followtheway.ru"
    echo "Using default server URL: $FOLLOWTHEWAY_SERVER_URL"
else
    echo "Using custom server URL: $FOLLOWTHEWAY_SERVER_URL"
fi

# Create ApiKeys.cs from template
echo "Creating ApiKeys.cs from template..."
if [ ! -f "src/Config/ApiKeys.cs.template" ]; then
    echo "ERROR: ApiKeys.cs.template not found!"
    exit 1
fi

# Replace placeholders in template
sed "s/{{FOLLOWTHEWAY_API_KEY_PLACEHOLDER}}/$FOLLOWTHEWAY_API_KEY/g; s|{{FOLLOWTHEWAY_SERVER_URL_PLACEHOLDER}}|$FOLLOWTHEWAY_SERVER_URL|g" \
    "src/Config/ApiKeys.cs.template" > "src/Config/ApiKeys.cs"

if [ ! -f "src/Config/ApiKeys.cs" ]; then
    echo "ERROR: Failed to create ApiKeys.cs!"
    exit 1
fi

echo "ApiKeys.cs created successfully!"

# Build the project
echo "Building FollowTheWay..."
dotnet build src/FollowTheWayPeak.csproj -c Release

if [ $? -ne 0 ]; then
    echo "ERROR: Build failed!"
    exit 1
fi

# Clean up - remove ApiKeys.cs for security
echo "Cleaning up ApiKeys.cs for security..."
rm "src/Config/ApiKeys.cs"

echo "========================================"
echo "Build completed successfully!"
echo "========================================"
echo ""
echo "IMPORTANT SECURITY NOTES:"
echo "- ApiKeys.cs was automatically deleted after build"
echo "- Your API key is embedded in the compiled DLL"
echo "- Never share your API key or commit ApiKeys.cs to version control"
echo "- The template file is safe to commit (contains only placeholders)"
echo ""