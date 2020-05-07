param(
    $ResourceGroupName = 'ha-test',
    $ResourceGroupLocation = 'eastus'
)

Write-Host 'Creating resource group.'
New-AzResourceGroup -Name $ResourceGroupName -Location $ResourceGroupLocation -Force

# Deploy the Azure Media Services instances.
Write-Host 'Running main ARM template deployment...'
$mainDeployment = New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile './ARMDeployment/main.json' -TemplateParameterFile 'ARMDeployment/all.parameters.json' -Verbose
$createdFunctionNames = $mainDeployment.Outputs['functionNames'].Value
Write-Host "Created following azure functions: $createdFunctionNames"
$functionFolders = @{JobScheduler="job-scheduler-function"; JobStatus="job-status-function"; StreamProvisioning="stream-provisioning-function"; JobVerification="job-verification-function"}

. dotnet publish media-services-high-availability.sln

foreach ($functionName in $createdFunctionNames)
{
    Write-Host "function name:" $functionName["fullName"].Value "function local folder:" $functionFolders[$functionName["function"].Value]
    $functionZipFilePath = (New-TemporaryFile).FullName + '.zip'
    $publishPath = $functionFolders[$functionName["function"].Value] + '/bin/Debug/netcoreapp3.1/publish/*'
    
    Write-Host "Creating zip file to publish from:" $publishPath 
    Compress-Archive -Path $publishPath -DestinationPath $functionZipFilePath -Force

    Write-Host "Publishing Azure Function:" $functionName["fullName"].Value
    Publish-AzWebApp -ResourceGroupName $ResourceGroupName -Name $functionName["fullName"].Value -ArchivePath $functionZipFilePath -Force
}

Write-Host 'Running event grid setup ARM template deployment...'
$eventGridDeployment = New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile './ARMDeployment/eventgridsetup.json' -TemplateParameterFile 'ARMDeployment/all.parameters.json' -Verbose