# email-fetch-attachment

## Metadata
- **skill-id**: email-fetch-attachment
- **category**: integration
- **executor**: csharp
- **version**: 1.0.0

## Description

Fetches attachment content from the email provider and stores it locally for processing. JustDoIt authority — reading has no external footprint.

## Authority

JustDoIt

## Parameters

- **externalMessageId**: The provider message ID containing the attachment (required)
- **contentId**: The provider attachment ID to fetch (required)
- **fileName**: The attachment file name for storage (required)
- **contentType**: The MIME type of the attachment (required)
- **referenceCode**: The reference code to associate with the stored attachment (required)
