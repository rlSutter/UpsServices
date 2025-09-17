# Configuration Template

This document provides a template for configuring the UPS Services application. Replace all placeholder values with your actual configuration values.

## Web.config Configuration

### App Settings
Replace the following placeholders in the `<appSettings>` section:

```xml
<!-- Database Configuration -->
<add key="dbuser" value="[CONFIGURE_DB_USER]" />
<add key="dbpass" value="[CONFIGURE_DB_PASSWORD]" />

<!-- UPS API Configuration -->
<add key="UPS_Username" value="[CONFIGURE_UPS_USERNAME]" />
<add key="UPS_Password" value="[CONFIGURE_UPS_PASSWORD]" />
<add key="UPS_Access_Key" value="[CONFIGURE_UPS_ACCESS_KEY]" />

<!-- Bitly API Configuration -->
<add key="BitLyUser" value="[CONFIGURE_BITLY_USER]" />
<add key="BitLyKey" value="[CONFIGURE_BITLY_KEY]" />

<!-- MaxMind GeoIP Configuration -->
<add key="GeoIP_license" value="[CONFIGURE_GEOIP_LICENSE]" />
<add key="GeoIP_userid" value="[CONFIGURE_GEOIP_USERID]" />

<!-- Google Translate API Configuration -->
<add key="GoogleAPIKey" value="[CONFIGURE_GOOGLE_API_KEY]" />

<!-- Twilio SMS Configuration -->
<add key="SMS_AccountSid" value="[CONFIGURE_TWILIO_ACCOUNT_SID]" />
<add key="SMS_AuthToken" value="[CONFIGURE_TWILIO_AUTH_TOKEN]" />
<add key="SMS_OurNumber" value="[CONFIGURE_TWILIO_PHONE_NUMBER]" />

<!-- Logging Configuration -->
<add key="remoteAddress" value="[CONFIGURE_SYSLOG_SERVER]" />
```

### Connection Strings
Replace the following placeholders in the `<connectionStrings>` section:

```xml
<add name="reports" connectionString="server=[DB_SERVER];uid=[DB_USER];pwd=[DB_PASSWORD];database=reports;Min Pool Size=3;Max Pool Size=5" providerName="System.Data.SqlClient" />
<add name="hcidb" connectionString="server=[DB_SERVER];uid=[DB_USER];pwd=[DB_PASSWORD];Min Pool Size=3;Max Pool Size=5;Connect Timeout=10;database=" providerName="System.Data.SqlClient" />
<add name="siebeldb" connectionString="server=[DB_SERVER];uid=[DB_USER];pwd=[DB_PASSWORD];database=siebeldb;Min Pool Size=3;Max Pool Size=5" providerName="System.Data.SqlClient" />
<add name="dms" connectionString="server=[DB_SERVER];uid=[DMS_USER];pwd=[DMS_PASSWORD];Min Pool Size=3;Max Pool Size=5;Connect Timeout=10;database=DMS" providerName="System.Data.SqlClient" />
```

## Code Configuration

### TrackWSClient.cs
Replace the following placeholders in the `TrackWSClient.cs` file:

```csharp
upssSvcAccessToken.AccessLicenseNumber = "[CONFIGURE_UPS_ACCESS_LICENSE]";
upssUsrNameToken.Username = "[CONFIGURE_UPS_USERNAME]";
upssUsrNameToken.Password = "[CONFIGURE_UPS_PASSWORD]";
tr.InquiryNumber = "[CONFIGURE_TEST_TRACKING_NUMBER]";
```

## Configuration Values Guide

### Database Configuration
- `[DB_SERVER]`: Your SQL Server instance name (e.g., "localhost", "server\instance")
- `[DB_USER]`: Database username with appropriate permissions
- `[DB_PASSWORD]`: Database password
- `[DMS_USER]`: DMS-specific database username
- `[DMS_PASSWORD]`: DMS-specific database password

### UPS API Configuration
- `[CONFIGURE_UPS_USERNAME]`: Your UPS API username
- `[CONFIGURE_UPS_PASSWORD]`: Your UPS API password
- `[CONFIGURE_UPS_ACCESS_KEY]`: Your UPS API access key
- `[CONFIGURE_UPS_ACCESS_LICENSE]`: Your UPS access license number

### Bitly API Configuration
- `[CONFIGURE_BITLY_USER]`: Your Bitly username
- `[CONFIGURE_BITLY_KEY]`: Your Bitly API key

### MaxMind GeoIP Configuration
- `[CONFIGURE_GEOIP_LICENSE]`: Your MaxMind GeoIP license key
- `[CONFIGURE_GEOIP_USERID]`: Your MaxMind user ID

### Google Translate API Configuration
- `[CONFIGURE_GOOGLE_API_KEY]`: Your Google Cloud API key with Translate API enabled

### Twilio SMS Configuration
- `[CONFIGURE_TWILIO_ACCOUNT_SID]`: Your Twilio Account SID
- `[CONFIGURE_TWILIO_AUTH_TOKEN]`: Your Twilio Auth Token
- `[CONFIGURE_TWILIO_PHONE_NUMBER]`: Your Twilio phone number (e.g., "+1234567890")

### Logging Configuration
- `[CONFIGURE_SYSLOG_SERVER]`: Your syslog server IP address or hostname

### Test Configuration
- `[CONFIGURE_TEST_TRACKING_NUMBER]`: A valid UPS tracking number for testing

## Security Notes

1. **Never commit actual credentials to version control**
2. **Use environment-specific configuration files**
3. **Consider using Azure Key Vault or similar for production secrets**
4. **Regularly rotate API keys and passwords**
5. **Use least-privilege database accounts**
6. **Enable SSL/TLS for all external API communications**

## Validation Checklist

Before deploying, ensure:
- [ ] All placeholder values have been replaced
- [ ] Database connections are tested
- [ ] External API credentials are valid
- [ ] Firewall rules allow outbound connections to external services
- [ ] SSL certificates are properly configured
- [ ] Logging is configured and working
- [ ] Error handling is properly configured

## Environment-Specific Configuration

### Development Environment
- Use local SQL Server instances
- Use development API keys with limited quotas
- Enable debug logging
- Use test phone numbers for SMS

### Staging Environment
- Use staging database servers
- Use staging API keys
- Enable comprehensive logging
- Test with real phone numbers (with consent)

### Production Environment
- Use production database servers with high availability
- Use production API keys with full quotas
- Enable error logging only
- Use production phone numbers
- Implement monitoring and alerting
