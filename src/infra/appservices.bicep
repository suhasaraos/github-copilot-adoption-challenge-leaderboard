// Variables
param appName string
param location string
param subnets array
param keyVaultName string
@secure()
param sqlConnStringSecret string
@secure()
param efConnStringSecret string
param storageAccountName string
param uniqueSuffix string
param sqlServerFqdn string
param sqlDatabaseName string
param allowedIpAddress string = ''

var allowIpAddressCidr = length(allowedIpAddress) > 0 ? '${allowedIpAddress}/32' : ''

resource appServicePrivateEndpoint 'Microsoft.Network/privateEndpoints@2022-07-01' = {
  name: '${appName}-webapp-pe'
  location: location
  properties: {
    subnet: {
      id: subnets[2].id // Use 'webapp-pe' subnet
    }
    privateLinkServiceConnections: [
      {
        name: 'webapp-connection'
        properties: {
          privateLinkServiceId: webApp.id
          groupIds: [ 'sites' ]
        }
      }
    ]
    customDnsConfigs: []
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'P1v2'
    tier: 'PremiumV2'
  }
  kind: 'linux'
  properties: {
    reserved: true // Linux
  }
}


resource webApp 'Microsoft.Web/sites@2024-11-01' = {
  name: '${appName}-${uniqueSuffix}'
  location: location
  kind: 'app,linux'
  tags: {
    'azd-service-name': 'web'
  }
  properties: {
    serverFarmId: appServicePlan.id
    virtualNetworkSubnetId: subnets[0].id // 'appsvc' subnet
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      scmType: 'VSTSRM'
      linuxFxVersion: 'DOTNETCORE|10.0'
      scmIpSecurityRestrictionsUseMain: false
      appSettings: [
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.privatelink.vaultcore.azure.net/secrets/${efConnStringSecret})'
        }
        {
          name: 'ConnectionStrings__SqlServer'
          value: 'Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'Database__Provider'
          value: 'SqlServer'
        }
        {
          name: 'sqlConnString'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.privatelink.vaultcore.azure.net/secrets/${sqlConnStringSecret})'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        // Azure AD Configuration (CallbackPath has default in appsettings.json)
        {
          name: 'AzureAd__CallbackPath'
          value: '/signin-oidc'
        }
        {
          name: 'AzureAd__ClientId'
          value: 'CONFIGURE-ME-AFTER-DEPLOYMENT'
        }
        {
          name: 'AzureAd__TenantId'
          value: 'CONFIGURE-ME-AFTER-DEPLOYMENT'
        }
        {
          name: 'AzureAd__Domain'
          value: 'CONFIGURE-ME-AFTER-DEPLOYMENT'
        }
        // GitHub Settings (disabled by default, matches appsettings.json)
        {
          name: 'GitHubSettings__Enabled'
          value: 'false'
        }
        // Challenge Settings (using defaults from appsettings.json)
        {
          name: 'ChallengeSettings__MaxParticipantsPerTeam'
          value: '8'
        }
        {
          name: 'ChallengeSettings__ChallengeStarted'
          value: 'false'
        }
        {
          name: 'ChallengeSettings__ChallengeStartDate'
          value: '10/04/2025'
        }
      ]
      alwaysOn: true
      ipSecurityRestrictions: concat(
        length(allowedIpAddress) > 0 ? [
          {
            action: 'Allow'
            priority: 50
            name: 'AllowUserIp'
            ipAddress: allowIpAddressCidr
          }
        ] : [],
        [
          {
            action: 'Deny'
            priority: 100
            name: 'DenyAll'
            ipAddress: '0.0.0.0/0'
          }
        ]
      )
      scmIpSecurityRestrictions: [
        {
          action: 'Allow' 
          priority: 100
          name: 'AllowAll'
          ipAddress: '0.0.0.0/0'
        }
      ]
    }
    httpsOnly: true
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource storageBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, webApp.name, 'StorageBlobDataContributor')
  scope: storageAccount
  properties: {
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}

output appServicePrivateEndpointName string = appServicePrivateEndpoint.name
output appServicePrivateEndpointIp string = appServicePrivateEndpoint.properties.customDnsConfigs[0].ipAddresses[0]
output appServiceDefaultHostName string = webApp.properties.defaultHostName
output webAppName string = webApp.name
output webAppPrincipalId string = webApp.identity.principalId
