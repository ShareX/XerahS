# XIP0027: Amazon S3 SSO Login + Auto-Provisioning via Custom Domain

**Status**: ✅ COMPLETE

## Goal Description
Add AWS IAM Identity Center (SSO) device login to the Amazon S3 plugin while keeping manual access keys.
In SSO mode, the user selects the bucket location (Endpoint / Region) and provides a Custom Domain; the bucket name is derived from the Custom Domain and auto-provisioned in the selected region (default `us-east-1`).

## Summary of Changes
- Add `S3AuthMode` to switch between **Access Keys** and **AWS SSO**.
- Store SSO client, token, and role credentials in the secret store.
- Implement AWS SSO OIDC device flow and SSO API calls.
- Add a shared SigV4 signer with session token support.
- Provision bucket + disable public access block via S3 API.
- Optional public bucket policy for read access (required for bucket-owner-enforced).

## Functional Requirements
1. **Access Keys Mode**: Current manual Access Key ID/Secret Access Key behavior remains unchanged.
2. **SSO Mode**:
   - Require Start URL + SSO Region.
   - Login via device authorization flow.
   - Select AWS account and role.
   - Select AWS S3 Endpoint / Region for bucket location.
   - Derive bucket name from Custom Domain and auto-provision it.
   - Optionally apply public bucket policy for `s3:GetObject`.
   - Use temporary role credentials (access key, secret key, session token).

## Key Files
- `src/Plugins/ShareX.AmazonS3.Plugin/S3ConfigModel.cs`
- `src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Uploader.cs`
- `src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Provider.cs`
- `src/Plugins/ShareX.AmazonS3.Plugin/ViewModels/AmazonS3ConfigViewModel.cs`
- `src/Plugins/ShareX.AmazonS3.Plugin/Views/AmazonS3ConfigView.axaml`

## Verification Plan
1. **Access Keys Regression**:
   - Load existing S3 config.
   - Upload a file successfully.
2. **SSO Login**:
   - Start device authorization, complete login, verify token saved.
3. **Account/Role Selection**:
   - Load accounts and roles; auto-select if only one.
4. **Auto-Provision**:
   - Enter Custom Domain, click Provision S3.
   - Verify bucket exists in the selected region and public access block disabled.
5. **Upload via SSO**:
   - Upload a file, ensure URL uses Custom Domain.
