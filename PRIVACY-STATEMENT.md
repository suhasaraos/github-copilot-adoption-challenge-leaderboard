# Security and Privacy Statement
## Microsoft Leaderboard App

This Leaderboard App is published by Microsoft as a public sample repository. Customers may review, adapt, and deploy the code in their own Azure environment. It is not an official Microsoft product or managed service. This document is not a formal Microsoft Privacy Statement; it describes the app's architecture, deployment model, and privacy/security considerations when deployed by customers.

## Customer-Controlled Deployment

The app is deployed entirely within your Azure environment, with privacy and security determined by how you implement and configure the setup. Microsoft provides best practice deployment guidance, but it is your responsibility to follow or adapt these according to your enterprise policies, guidelines, and standards. No data is shared with or exfiltrated to Microsoft or any third party. All application data—including participant details, scores, and metrics—remains within your Azure subscription.

## No External Data Sharing

Microsoft does not collect, transmit, or access any app data. Data may be shared with Microsoft only if you voluntarily provide feedback or usage data outside of the app, in line with the Microsoft Privacy Statement.

## Non-Production Deployment

The app is intended solely for non-production use, with a limited lifetime tied to the challenge duration. After the challenge concludes, you are responsible for deleting the app, associated deployments, and any related binaries or artifacts.

## Personally Identifiable Information (PII)

The app processes only the information required for leaderboard functionality (e.g., participant names, team assignments) as entered by users. No additional PII is collected by the app and no data is stored by Microsoft since it is deployed into customer environment. Authentication and user profile details leverage Microsoft Entra ID (formerly Azure Active Directory) and remain within your environment.

## Authentication & Secrets Management

The app uses Microsoft Entra ID for authentication. All secrets (e.g., GitHub PATs, SMTP passwords) are securely stored in Azure Key Vault when Microsoft's deployment guidance is followed, accessed via system-assigned managed identities with strict access controls.

## GitHub Security Principles

While users may perform challenges on production code, the app never accesses, processes, or transmits production code or repository content. It only queries the GitHub REST APIs for Teams and Copilot metrics data (usage statistics, activity counts) in line with GitHub Copilot security principles and the Copilot Trust Center. No source code, IP, or confidential business information is transmitted or processed by the app.

## Network & Privacy Controls

Microsoft recommends using VNet Integration, private endpoints for Azure SQL and Key Vault, and strict firewall rules. You must ensure the deployment remains private and no services are exposed publicly. All required outbound traffic (e.g., to GitHub or MS Learn APIs) is encrypted via TLS.

## Important Note

Privacy and security are ultimately determined by how you deploy and configure the application. While Microsoft provides best practice guidance, you must ensure compliance with all applicable organization-specific and regulatory requirements at the country, state, and local levels, and maintain a fully private deployment with no publicly exposed services.