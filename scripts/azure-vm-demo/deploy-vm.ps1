# ============================================================================
# Tech4Logic Video Search - Azure VM Demo Deployment (Cheapest POC)
# ============================================================================
# Creates: Resource group + Ubuntu 22.04 B2s VM with meaningful tags.
# Run from repo root or scripts/azure-vm-demo. Requires: az login
# ============================================================================

$ErrorActionPreference = "Stop"

$RG_NAME   = "t4l-demo-rg"
$VM_NAME   = "t4l-demo-vm"
$LOCATION  = "eastus"
$IMAGE     = "Ubuntu2204"
$VM_SIZE   = "Standard_B2s"
$ADMIN_USER = "t4ldemo"   # lowercase required by Azure for Linux VM
# Use Standard public IP if your subscription has Basic SKU quota of 0
$PUBLIC_IP_SKU = "Standard"

# Meaningful tags for cost tracking and identification
$TAGS = @{
  "project"     = "t4l-videosearch"
  "environment" = "demo"
  "purpose"     = "video-search-poc-demo"
  "application" = "t4l-videostreaming"
  "managedBy"   = "script"
  "workload"    = "docker-compose"
  "role"        = "demo-server"
}

$TAGS_STRING = ($TAGS.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join " "

Write-Host "Creating resource group: $RG_NAME in $LOCATION with tags..." -ForegroundColor Cyan
az group create --name $RG_NAME --location $LOCATION --tags $TAGS_STRING

Write-Host "Creating VM: $VM_NAME ($VM_SIZE, $IMAGE) with tags..." -ForegroundColor Cyan
az vm create `
  --resource-group $RG_NAME `
  --name $VM_NAME `
  --image $IMAGE `
  --size $VM_SIZE `
  --public-ip-sku $PUBLIC_IP_SKU `
  --admin-username $ADMIN_USER `
  --generate-ssh-keys `
  --tags $TAGS_STRING

Write-Host "Opening ports 80, 443 (reverse proxy); 3000, 5000 optional..." -ForegroundColor Cyan
az vm open-port --resource-group $RG_NAME --name $VM_NAME --port 80   --priority 1001
az vm open-port --resource-group $RG_NAME --name $VM_NAME --port 443   --priority 1002
az vm open-port --resource-group $RG_NAME --name $VM_NAME --port 3000  --priority 1003
az vm open-port --resource-group $RG_NAME --name $VM_NAME --port 5000  --priority 1004

$PUBLIC_IP = (az vm show -g $RG_NAME -n $VM_NAME -d --query publicIps -o tsv)
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Deployment complete." -ForegroundColor Green
Write-Host "Public IP: $PUBLIC_IP" -ForegroundColor Green
Write-Host "SSH: ssh ${ADMIN_USER}@$PUBLIC_IP" -ForegroundColor Green
Write-Host "Web (after setup): http://$PUBLIC_IP" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next: Run vm-setup.sh on the VM (see docs/AZURE-POC-HOSTING.md)" -ForegroundColor Yellow
