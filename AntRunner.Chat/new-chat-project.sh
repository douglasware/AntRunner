#!/bin/bash

# Script to deploy template with optional environment variable overrides and volume mapping updates

set -e

# Function to prompt for environment variable override
prompt_env_var() {
  local var_name=$1
  local current_value=$2
  echo "Current value of $var_name is: ${current_value:-<not set>}"
  read -p "Enter new value for $var_name (leave blank to keep current): " input_value
  if [ -n "$input_value" ]; then
    echo "$input_value"
  else
    echo "$current_value"
  fi
}

# Function to prompt yes/no question
prompt_yes_no() {
  local prompt_msg=$1
  while true; do
    read -p "$prompt_msg (y/n): " yn
    case $yn in
      [Yy]* ) echo "yes"; break;;
      [Nn]* ) echo "no"; break;;
      * ) echo "Please answer y or n.";;
    esac
  done
}

# 1. Collect target directory from user
read -p "Enter the target directory where template will be copied: " TARGET_DIR
if [ -z "$TARGET_DIR" ]; then
  echo "Target directory is required. Exiting."
  exit 1
fi

# 2. Copy template folders to target directory
# Assuming the script is in the same folder as the template folder
SCRIPT_DIR=$(dirname "$0")
cp -r "$SCRIPT_DIR/template"/* "$TARGET_DIR"

# 3. Override environment variables in docker-compose.yaml if requested
DOCKER_COMPOSE_FILE="$TARGET_DIR/Sandboxes/code-interpreter/docker-compose.yaml"
if [ ! -f "$DOCKER_COMPOSE_FILE" ]; then
  echo "docker-compose.yaml not found at $DOCKER_COMPOSE_FILE. Exiting."
  exit 1
fi

# Read current env values from docker-compose.yaml (if any)
CURRENT_AZURE_OPENAI_RESOURCE=$(grep -oP '(?<=AZURE_OPENAI_RESOURCE: ).*' "$DOCKER_COMPOSE_FILE" || echo "")
CURRENT_AZURE_OPENAI_API_KEY=$(grep -oP '(?<=AZURE_OPENAI_API_KEY: ).*' "$DOCKER_COMPOSE_FILE" || echo "")
CURRENT_AZURE_OPENAI_DEPLOYMENT=$(grep -oP '(?<=AZURE_OPENAI_DEPLOYMENT: ).*' "$DOCKER_COMPOSE_FILE" || echo "")

echo "You can override the following environment variables in docker-compose.yaml:"
AZURE_OPENAI_RESOURCE=$(prompt_env_var "AZURE_OPENAI_RESOURCE" "$CURRENT_AZURE_OPENAI_RESOURCE")
AZURE_OPENAI_API_KEY=$(prompt_env_var "AZURE_OPENAI_API_KEY" "$CURRENT_AZURE_OPENAI_API_KEY")
AZURE_OPENAI_DEPLOYMENT=$(prompt_env_var "AZURE_OPENAI_DEPLOYMENT" "$CURRENT_AZURE_OPENAI_DEPLOYMENT")

# Use sed to update or add environment variables in docker-compose.yaml
update_or_add_env_var() {
  local var=$1
  local val=$2
  local file=$3
  # If variable exists, replace its value
  if grep -q "$var:" "$file"; then
    sed -i "s|$var:.*|$var: $val|" "$file"
  else
    # Add variable under environment: section (assumes environment: exists)
    sed -i "/environment:/a\    $var: $val" "$file"
  fi
}

update_or_add_env_var "AZURE_OPENAI_RESOURCE" "$AZURE_OPENAI_RESOURCE" "$DOCKER_COMPOSE_FILE"
update_or_add_env_var "AZURE_OPENAI_API_KEY" "$AZURE_OPENAI_API_KEY" "$DOCKER_COMPOSE_FILE"
update_or_add_env_var "AZURE_OPENAI_DEPLOYMENT" "$AZURE_OPENAI_DEPLOYMENT" "$DOCKER_COMPOSE_FILE"

# 4. Optionally update volume mappings in docker-compose.yaml
update_volumes=$(prompt_yes_no "Do you want to update the volume mappings in docker-compose.yaml to the target directory paths?")

if [ "$update_volumes" = "yes" ]; then
  # Compute absolute paths for replacement
  ABS_NOTEBOOKS_SHARED_CONTENT="$(realpath "$TARGET_DIR/Notebooks/shared-content")"
  ABS_ASSISTANT_DEFINITIONS="$(realpath "$TARGET_DIR/Notebooks/AssistantDefinitions")"

  # Escape slashes for sed
  ESC_NOTEBOOKS_SHARED_CONTENT=$(echo "$ABS_NOTEBOOKS_SHARED_CONTENT" | sed 's/\//\\//g')
  ESC_ASSISTANT_DEFINITIONS=$(echo "$ABS_ASSISTANT_DEFINITIONS" | sed 's/\//\\//g')

  # Update volume mappings in docker-compose.yaml
  sed -i "s|\.\./\.\./Notebooks/shared-content:/app/shared/content|$ESC_NOTEBOOKS_SHARED_CONTENT:/app/shared/content|g" "$DOCKER_COMPOSE_FILE"
  sed -i "s|\.\./\.\./Notebooks/AssistantDefinitions:/app/AssistantDefinitions|$ESC_ASSISTANT_DEFINITIONS:/app/AssistantDefinitions|g" "$DOCKER_COMPOSE_FILE"
else
  echo "Skipping volume mapping updates."
fi

# 5. Execute docker compose down and up
echo "Stopping existing containers..."
docker compose -f "$DOCKER_COMPOSE_FILE" down

echo "Starting containers with updated configuration..."
docker compose -f "$DOCKER_COMPOSE_FILE" up -d

echo "Deployment completed successfully."
