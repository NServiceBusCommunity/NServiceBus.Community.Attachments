# FileShare Attachments

Uses a file share to store attachments for messages.


## Usage

Two settings are required as part of the default usage:

 * A file share or directory location.
 * A default time to keep for attachments.

snippet: FileShareEnableAttachments


### Recommended Usage

Uses the `NServiceBus.Attachments.FileShare.TimeToKeep.Default` method for attachment cleanup.

This usage results in the following:

snippet: FileShareEnableAttachmentsRecommended


include: attachments
