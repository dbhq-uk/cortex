# email-send

## Metadata
- **skill-id**: email-send
- **category**: integration
- **executor**: csharp
- **version**: 1.0.0

## Description

Sends an outbound email via the configured email provider. Requires AskMeFirst authority — sending email has external footprint.

## Authority

AskMeFirst

## Parameters

- **to**: Recipient email address (required)
- **subject**: Email subject line (required)
- **body**: Email body content (required)
- **cc**: Comma-separated CC addresses (optional)
- **inReplyToExternalId**: External ID of email being replied to, for threading (optional)
