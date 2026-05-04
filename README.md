# SupMail

A Windows desktop application that solves the attachment delivery problem for cloud-hosted Priority ERP systems. SupMail bridges the gap between cloud-based Priority instances and local file storage by enabling users to fetch purchase order attachments from local directories and send them via Outlook.

## The Problem

When Priority ERP is hosted on the cloud, the server cannot access local machine drives (e.g., `P:\`) where attachment files are typically stored. This causes Priority's Quick Mail and built-in mail functions to fail because:
- Attachment paths reference local directories (e.g., `P:\DATA\MD\MMC-0261_MD_REV-A.PDF`)
- The Priority server has no visibility into local client drives
- Email attachments cannot be included server-side

## The Solution

SupMail runs on a client machine with access to the local attachment directories and:
1. Fetches purchase order details and attachment metadata from the Priority API
2. Allows users to select which local attachments to include
3. Creates and populates Outlook email drafts with the selected files
4. Enables reliable email delivery with all necessary attachments

## Features

- **Priority ERP Integration**: Fetches purchase order data directly from your Priority ERP system via REST API
- **Smart File Selection**: Browse and select which attachments to include before sending
- **Batch Operations**: Zip selected files together for easier management and sending
- **Outlook Integration**: Automatically creates and populates Outlook email drafts with selected attachments
- **Secure Credentials**: Stores API credentials securely with basic authentication
- **Connection Testing**: Test your Priority API connection before use
- **Error Logging**: Comprehensive error reporting saved to your desktop for troubleshooting

## System Requirements

- Windows 7 or later
- .NET 8.0 Runtime
- Microsoft Outlook (for email sending functionality)
- Network access to cloud-hosted Priority ERP system
- **Local access to the attachment directory** (e.g., `P:\` drive or equivalent network/local path)
- The local attachment path must be mapped and accessible from the machine running SupMail

## Installation

1. Download the latest release from the [Releases](https://github.com/lioredri/SupMail/releases) page
2. Extract the application files
3. Run `SupMail.exe`
4. Configure your Priority API settings in the Settings dialog

## Configuration

Before using SupMail, you need to configure your cloud-hosted Priority API connection:

1. Click the **Settings** button in the main window
2. Enter your Priority API Base URL (e.g., `https://t.eu.priority-connect.online/odata/Priority/tabbtd38.ini/usdemo`)
3. Enter your Priority API username
4. Enter your Priority API password
5. Click **Test Connection** to verify the settings are correct
6. Click **Save** to store your configuration

**Important**: Ensure the machine running SupMail has access to the local paths where attachments are stored (e.g., `P:\`).

Settings are stored in:
```
%APPDATA%\SupMail\settings.json
```

## Usage

### Basic Workflow

1. **Enter Purchase Order Number**: In the main window, type the document number of the purchase order you want to process
2. **Click Process**: SupMail connects to Priority and retrieves the order details and attachments
3. **Select Files**: A dialog appears showing all available attachments for the purchase order
   - Use the checkboxes to select which files to include
   - Use "Select All" to quickly include all attachments
4. **Zip or Send**:
   - Click **Zip Selected** to create a compressed archive of selected files
   - Click **Attach Selected** to create an Outlook email draft with the selected attachments
5. **Send Email**: Outlook opens with the email draft ready to send

### File Selection Options

- **Individual Selection**: Check/uncheck specific files using the checkboxes
- **Select All**: Quickly select or deselect all files at once
- **View File Sizes**: See the size of each attachment before selecting

## Technical Details

### Architecture

- **Frontend**: WPF (Windows Presentation Foundation) with XAML
- **Backend**: .NET 8.0
- **External Integration**: 
  - Priority ERP REST API for data retrieval
  - Microsoft Outlook COM Interop for email creation
  - File compression for batch attachments

### Key Components

- **MainWindow**: Primary UI for document number entry and processing
- **FileSelectionWindow**: Dialog for selecting attachments before sending
- **SettingsWindow**: Configuration interface for API credentials
- **AppSettings**: Settings management and persistence
- **App**: Application initialization and error handling

### Data Flow

```
Document Number Input
    ↓
Cloud Priority API Request (Basic Auth)
    ↓
Parse JSON Response (Order Details + Attachment Metadata)
    ↓
Resolve Local File Paths (P:\ or network paths)
    ↓
Display File Selection Dialog
    ↓
User Selects Files
    ↓
Create/Populate Outlook Email Draft with Local Files
    ↓
User Sends via Outlook
```

### File Resolution

SupMail handles multiple attachment source types:
- **Local Paths**: Direct file system paths (e.g., `P:\DATA\MD\...`)
- **System URLs**: Files uploaded to Priority (e.g., `../../system/mail/...`)

## Error Handling

If the application crashes, an error log is automatically created on your desktop:
```
C:\Users\[YourUsername]\Desktop\SupMail_Error.txt
```

This file contains detailed information about what went wrong, including:
- Timestamp of the error
- Error message and source
- Full stack trace
- Inner exception details (if applicable)

## Dependencies

- **Newtonsoft.Json 13.0.4**: For JSON parsing and serialization
- **System.Net.Http**: For API communication
- **Microsoft Office Interop (Outlook)**: For Outlook integration (COM)

## Version

Current Version: **1.2**

## License

This project is available on GitHub at [https://github.com/lioredri/SupMail](https://github.com/lioredri/SupMail)

## Support

For issues, feature requests, or contributions, please visit the [GitHub repository](https://github.com/lioredri/SupMail).

---

**Note**: This application is specifically designed for cloud-hosted Priority ERP instances where local attachment paths are not accessible from the server. The machine running SupMail must have access to the local or network paths where attachments are stored (typically `P:\` drive or equivalent).
