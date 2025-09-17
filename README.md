# UPS Services - ASP.NET Web Services Platform

## Overview

This is a comprehensive ASP.NET web services platform that provides integration with multiple third-party services including UPS tracking, Bitly link shortening, Twilio SMS, MaxMind GeoIP, and Google Translate. The platform serves as a centralized service layer for various business operations.

## Architecture

The solution is built using:
- **Framework**: ASP.NET Web Forms (.NET Framework 4.7.2)
- **Language**: C#
- **Database**: SQL Server
- **Logging**: log4net
- **Caching**: System.Runtime.Caching

## Core Services

### 1. UPS Tracking Service Integration
- **Purpose**: Integrates with UPS tracking API to provide package tracking functionality
- **Key Features**:
  - Real-time package tracking
  - SOAP-based communication with UPS services
  - Comprehensive error handling and logging
  - Support for multiple tracking request types

### 2. Bitly Link Management Service
- **Purpose**: Abstracts and enhances Bitly's URL shortening service with management and reporting capabilities
- **Key Features**:
  - URL shortening with custom descriptions
  - Link tracking and analytics
  - Click-through monitoring
  - Geographic distribution reporting
  - Link management (creation, deletion, updates)

### 3. Twilio SMS Integration
- **Purpose**: Provides SMS messaging capabilities through Twilio's API
- **Key Features**:
  - Send SMS messages programmatically
  - Support for international numbers
  - Delivery status tracking
  - Error handling and retry logic

### 4. MaxMind GeoIP Service
- **Purpose**: Provides IP address geolocation services
- **Key Features**:
  - IP address to geographic location mapping
  - Country, state, city, and postal code resolution
  - Latitude/longitude coordinates
  - Caching for improved performance

### 5. Google Translate Integration
- **Purpose**: Provides text translation services using Google Translate API
- **Key Features**:
  - Multi-language text translation
  - Language code validation
  - Cached language mappings
  - Support for various source and target languages

## Database Schema

The application uses multiple SQL Server databases:

### Reports Database
- **TRACKING_LINKS**: Stores shortened URLs and metadata
- **TRACKING_IPS**: Caches IP address geolocation data
- **TRACKING_CLICKS**: Records click-through analytics

### Siebel Database
- **S_LANG**: Language codes and translation mappings

See `database_schema.sql` for complete DDL scripts.

## Project Structure

```
UPSServices/
├── App_Code/                    # Core business logic
│   ├── WebService.cs           # Main web service implementation
│   ├── CachingWrapper.cs       # Caching utilities
│   ├── IsMail.cs              # Email validation utilities
│   └── TrackWSClient.cs       # UPS tracking client
├── Account/                    # Authentication pages
├── App_Data/                   # Application data
├── App_WebReferences/          # Web service references
├── Bin/                        # Compiled assemblies
├── Scripts/                    # JavaScript libraries
├── Styles/                     # CSS stylesheets
├── temp/                       # Temporary files
├── Web.config                  # Application configuration
├── WebService.asmx            # Web service endpoint
└── Default.aspx               # Default page
```

## Configuration

### Web.config Settings

The application requires several configuration settings in `Web.config`:

#### Connection Strings
```xml
<connectionStrings>
  <add name="reports" connectionString="..." />
  <add name="hcidb" connectionString="..." />
  <add name="siebeldb" connectionString="..." />
  <add name="dms" connectionString="..." />
</connectionStrings>
```

#### App Settings
```xml
<appSettings>
  <!-- UPS Configuration -->
  <add key="UPS_Username" value="..." />
  <add key="UPS_Password" value="..." />
  <add key="UPS_Access_Key" value="..." />
  
  <!-- Bitly Configuration -->
  <add key="BitLyUser" value="..." />
  <add key="BitLyKey" value="..." />
  
  <!-- Twilio Configuration -->
  <add key="SMS_AccountSid" value="..." />
  <add key="SMS_AuthToken" value="..." />
  <add key="SMS_OurNumber" value="..." />
  
  <!-- MaxMind Configuration -->
  <add key="GeoIP_license" value="..." />
  <add key="GeoIP_userid" value="..." />
  
  <!-- Google Translate Configuration -->
  <add key="GoogleAPIKey" value="..." />
</appSettings>
```

## Key Web Service Methods

### LinkMaker
Creates shortened URLs using Bitly service with tracking capabilities.

**Parameters:**
- `tURL`: Target URL to shorten
- `Description`: Description for the link
- `TypeCD`: Type classification code

**Returns:**
- Shortened URL with tracking key
- Database record ID for future reference

### RemoveLink
Removes a tracking link and associated data.

**Parameters:**
- `tURL`: URL to remove
- `Key`: Tracking key (optional)

### GeoIP
Provides geolocation information for an IP address.

**Parameters:**
- `ipaddress`: IP address to lookup

**Returns:**
- Country, state, city, postal code
- Latitude and longitude coordinates

### SendSMS
Sends SMS messages via Twilio.

**Parameters:**
- `PhoneNumber`: Destination phone number
- `Message`: SMS message content

### TranslateText
Translates text between languages using Google Translate.

**Parameters:**
- `Text`: Text to translate
- `SrcLang`: Source language code
- `DestLang`: Destination language code

## Dependencies

### NuGet Packages
- **BitlyDotNET**: Bitly API integration
- **Google.Apis.Translate.v2**: Google Translate API
- **MaxMind.GeoIP2**: MaxMind GeoIP database
- **Newtonsoft.Json**: JSON serialization
- **RestSharp**: HTTP client library
- **Twilio**: Twilio SMS API
- **log4net**: Logging framework

### External Services
- UPS Tracking API
- Bitly API
- Twilio SMS API
- MaxMind GeoIP Database
- Google Translate API

## Logging

The application uses log4net for comprehensive logging:

- **Remote Syslog**: Centralized logging to syslog server
- **File Logging**: Local file-based logging with rotation
- **Debug Logging**: Detailed debug information
- **Error Tracking**: Exception and error logging

## Caching Strategy

The application implements a multi-layer caching strategy:

1. **Memory Cache**: For frequently accessed data (language codes, etc.)
2. **Database Cache**: For IP geolocation data
3. **Service Cache**: For external API responses

## Security Considerations

- All external API credentials are stored in configuration
- Database connections use parameterized queries
- Input validation for all user inputs
- SQL injection prevention
- XSS protection through proper encoding

## Performance Optimizations

- Connection pooling for database connections
- Caching of frequently accessed data
- Asynchronous operations where applicable
- Efficient database indexing
- Log file rotation to prevent disk space issues

## Deployment Requirements

### Server Requirements
- Windows Server with IIS
- .NET Framework 4.7.2
- SQL Server (2012 or later)

### Database Setup
1. Run the provided `database_schema.sql` script
2. Configure connection strings in `Web.config`
3. Set up appropriate database permissions

### External Service Setup
1. Obtain API keys for all external services
2. Configure web service endpoints
3. Set up proper firewall rules for external API access

## Monitoring and Maintenance

### Health Checks
- Database connectivity monitoring
- External service availability checks
- Log file size monitoring
- Performance metrics tracking

### Maintenance Tasks
- Regular database cleanup (old tracking data)
- Log file rotation and archival
- Cache invalidation and refresh
- API key rotation and updates

## Error Handling

The application implements comprehensive error handling:

- **Service-Level Errors**: Graceful degradation when external services are unavailable
- **Database Errors**: Connection retry logic and fallback mechanisms
- **Validation Errors**: Input validation with detailed error messages
- **Logging**: All errors are logged with context information

## API Documentation

### SOAP Endpoints
- `WebService.asmx`: Main web service endpoint
- WSDL available at: `WebService.asmx?WSDL`

### HTTP Methods
- GET: Service discovery and health checks
- POST: SOAP service calls

## Troubleshooting

### Common Issues

1. **Database Connection Failures**
   - Check connection strings in `Web.config`
   - Verify database server accessibility
   - Confirm user permissions

2. **External API Failures**
   - Verify API credentials
   - Check network connectivity
   - Review API rate limits

3. **Performance Issues**
   - Monitor database query performance
   - Check cache hit rates
   - Review log file sizes

### Debug Mode
Enable debug logging by setting debug flags in `Web.config`:
```xml
<add key="LinkMaker_debug" value="Y" />
<add key="GeoIP_debug" value="Y" />
<add key="SendSMS_debug" value="Y" />
```

## Support and Maintenance

For technical support or maintenance requests:
1. Check application logs for error details
2. Verify external service status
3. Review database connectivity
4. Check configuration settings

## Version History

- **v1.0**: Initial release with core UPS tracking functionality
- **v1.1**: Added Bitly integration and link management
- **v1.2**: Integrated Twilio SMS services
- **v1.3**: Added MaxMind GeoIP capabilities
- **v1.4**: Integrated Google Translate services
- **v1.5**: Enhanced caching and performance optimizations

## License

This software is proprietary and confidential. All rights reserved.

---

**Note**: This application integrates with multiple third-party services. Ensure all necessary API keys and credentials are properly configured before deployment. Regular monitoring of external service status and API rate limits is recommended.
