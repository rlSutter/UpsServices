using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Web.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using UPSTrackService;
using Microsoft.VisualBasic;
using System.Web.Script.Services.UPS;
using BitlyDotNET.Implementations;
using BitlyDotNET.Interfaces;
using BitlyDotNET.Exceptions;
using log4net;
using MaxMind.GeoIP2;
using System.ComponentModel;
using Google.Apis.Translate.v2;
using Google.Apis.Translate.v2.Data;
using Google.Apis.Discovery;
using Google.Apis.Services;
using Newtonsoft.Json;
using TranslationsResource = Google.Apis.Translate.v2.Data.TranslationsResource;
using System.Reflection;
using System.Reflection.Emit;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

// ============================================
// This namespace contains functionality to capture SOAP data
namespace ExtractSOAPtoXML
{
    /// <summary>
    /// Adds the ability to retrieve the SOAP request/response.
    /// </summary>
    public class ServiceSpy : TrackResponse
    {
        private StreamSpy writerStreamSpy;
        private XmlTextWriter xmlWriter;

        private StreamSpy readerStreamSpy;
        private XmlTextReader xmlReader;

        public MemoryStream WriterStream
        {
            get { return writerStreamSpy == null ? null : writerStreamSpy.ClonedStream; }
        }

        public XmlTextWriter XmlWriter
        {
            get { return xmlWriter; }
        }

        public MemoryStream ReaderStream
        {
            get { return readerStreamSpy == null ? null : readerStreamSpy.ClonedStream; }
        }

        public XmlTextReader XmlReader
        {
            get { return xmlReader; }
        }

        //protected override void Dispose(bool disposing)
        //{
        //    base.Dispose(disposing);
        //    DisposeWriterStreamSpy();
        //    DisposeReaderStreamSpy();
        //}

        protected XmlWriter GetWriterForMessage(SoapClientMessage message, int bufferSize)
        {
            // Dispose previous writer stream spy.
            DisposeWriterStreamSpy();

            writerStreamSpy = new StreamSpy(message.Stream);
            // XML should always support UTF8.
            xmlWriter = new XmlTextWriter(writerStreamSpy, Encoding.UTF8);

            return xmlWriter;
        }

        protected XmlReader GetReaderForMessage(SoapClientMessage message, int bufferSize)
        {
            // Dispose previous reader stream spy.
            DisposeReaderStreamSpy();

            readerStreamSpy = new StreamSpy(message.Stream);
            xmlReader = new XmlTextReader(readerStreamSpy);

            return xmlReader;
        }

        private void DisposeWriterStreamSpy()
        {
            if (writerStreamSpy != null)
            {
                writerStreamSpy.Dispose();
                writerStreamSpy.ClonedStream.Dispose();
                writerStreamSpy = null;
            }
        }

        private void DisposeReaderStreamSpy()
        {
            if (readerStreamSpy != null)
            {
                readerStreamSpy.Dispose();
                readerStreamSpy.ClonedStream.Dispose();
                readerStreamSpy = null;
            }
        }

        /// <summary>
        /// Wrapper class to clone read/write bytes.
        /// </summary>
        public class StreamSpy : Stream
        {
            private Stream wrappedStream;
            private long startPosition;
            private MemoryStream clonedStream = new MemoryStream();

            public StreamSpy(Stream wrappedStream)
            {
                this.wrappedStream = wrappedStream;
                startPosition = wrappedStream.Position;
            }

            public MemoryStream ClonedStream
            {
                get { return clonedStream; }
            }

            public override bool CanRead
            {
                get { return wrappedStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return wrappedStream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return wrappedStream.CanWrite; }
            }

            public override void Flush()
            {
                wrappedStream.Flush();
            }

            public override long Length
            {
                get { return wrappedStream.Length; }
            }

            public override long Position
            {
                get { return wrappedStream.Position; }
                set { wrappedStream.Position = value; }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                long relativeOffset = wrappedStream.Position - startPosition;
                int result = wrappedStream.Read(buffer, offset, count);
                if (clonedStream.Position != relativeOffset)
                {
                    clonedStream.Position = relativeOffset;
                }
                clonedStream.Write(buffer, offset, result);
                return result;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return wrappedStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                wrappedStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                long relativeOffset = wrappedStream.Position - startPosition;
                wrappedStream.Write(buffer, offset, count);
                if (clonedStream.Position != relativeOffset)
                {
                    clonedStream.Position = relativeOffset;
                }
                clonedStream.Write(buffer, offset, count);
            }

            public override void Close()
            {
                wrappedStream.Close();
                base.Close();
            }

            protected override void Dispose(bool disposing)
            {
                if (wrappedStream != null)
                {
                    wrappedStream.Dispose();
                    wrappedStream = null;
                }
                base.Dispose(disposing);
            }
        }
    }
}

// ============================================
// This Namespace contains the UPS web services
namespace System.Web.Script.Services.UPS
{
    [WebService(Namespace = "http://cloudsvc.certegrity.com/ups/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ScriptService]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]

    // ============================================
    // Class for handling certificate trust
    public class TrustAllCertificatePolicy : System.Net.ICertificatePolicy
    {
        public TrustAllCertificatePolicy() { }
        public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem)
        {
            return true;
        }
    }

    // ============================================
    // Main class for services
    [WebService(Namespace = "http://cloudsvc.certegrity.com/ups/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ScriptService]
    public class UPSServices : System.Web.Services.WebService
    {
        // Globals
        System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();

        // ============================================
        // Initialize services
        public UPSServices()
        {
            //Uncomment the following line if using designed components 
            //InitializeComponent(); 
        }

        // ============================================
        // Verify Email Address using IsMail
        [WebMethod(Description = "Verify an email address")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public Boolean VerifyEmail(string sEmail, string Debug)
        {
            // This webservice validates that an email address is valid using IsMail

            // The parameters are as follows:

            //          sEmail  - The email address to validate
            //          Debug   - A flag t o indicate the service is to be run in debug mode
            //                    "Y" - Yes for debug mode on
            //                    "N" - Yes for debuG mode off
            //                    "T" - Yes for test mode on

            // The results are returned as an XML document, similar to the following:
            //      <boolean xmlns="http://cloudsvc.certegrity.com/ups/">true</boolean>

            // ============================================
            // Declarations
            //  Generic        
            string errmsg = "";
            Boolean bresults = false;

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string callip = Context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            string VersionNum = "101";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();

            // ============================================
            // Variable Setup
            Logging = "Y";
            if (Debug == "T") { sEmail = "techsupport@gettips.com"; }
            if (callip == null) {
                callip = Context.Request.UserHostAddress;
            }
            else {
                if (callip.Contains(",")) {
                    callip = callip.Substring(0,callip.IndexOf(",")-1);
                }
            }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\VerifyEmail.log";
                log4net.GlobalContext.Properties["LogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  sEmail: " + sEmail + "\r\n");
                }
            }

            // Perform email check
            IsEMail IsM = null;
            IsM = new IsEMail();

            bresults = IsM.IsEmailValid(sEmail);

            // ============================================
            // Close the log file, if any
            try
            {
                DateTime et = DateTime.Now;
                if (errmsg != "") { 
                    ltemp = "At " + et.ToString() + ", Error(s): " + errmsg + ".. for address " + sEmail + ", invoked from " + callip;
                } else {
                    ltemp = "At " + et.ToString() + ", verified address " + sEmail + ", invoked from " + callip;
                }
                if (Debug != "T")
                {
                    if (Debug != "Y")
                    {
                        debuglog.Debug(ltemp);
                    }
                    else
                    {
                        debuglog.Debug("Results: " + bresults);
                    }
                }
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "" && errmsg != "No error") { debuglog.Debug("\r\n Error: " + errmsg); }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended at " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("VerifyEmail Results " + ltemp);
                if (errmsg != "" && errmsg != "No error") { eventlog.Error("VerifyEmail error " + errmsg); }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            return bresults;
        }

        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // Send an SMS Message using Twilio
        [WebMethod(Description = "Send an SMS Message")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public Boolean SendSMS(string CellNumber, string Message, string Debug)
        {
            // This webservice sends an SMS message to the phone number specified

            // The parameters are as follows:

            // 	CellNumber 	- The cell number to send to
            //	Message 	- The message to send to that number	
            //	Debug   	- A flag to indicate the service is to be run in debug mode
            //                 		"Y" - Yes for debug mode on
            //                 		"N" - Yes for debug mode off

            // The results are returned as an XML document, similar to the following:
            //      <boolean xmlns="http://cloudsvc.certegrity.com/ups/">true</boolean>

            // ============================================
            // Declarations
            //  Generic        
            string errmsg = "";
            string temp = "";
            Boolean bresults = false;
            string AccountSid = "";
            string AuthToken = "";
            string OurNumber = "";

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string callip = Context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            string VersionNum = "102";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();

            // ============================================
            // Variable Setup
            Logging = "Y";
            if (callip == null)
            {
                callip = Context.Request.UserHostAddress;
            }
            else
            {
                if (callip.Contains(","))
                {
                    callip = callip.Substring(0, callip.IndexOf(",") - 1);
                }
            }

            // ============================================
            // Get Application Keys
            try
            {
                AccountSid = System.Web.Configuration.WebConfigurationManager.AppSettings["SMS_AccountSid"];
                if (AccountSid == "") { AccountSid = "ACc6ae8130e0021f42bc2faaf3e601507e"; }
                AuthToken = System.Web.Configuration.WebConfigurationManager.AppSettings["SMS_AuthToken"];
                if (AuthToken == "") { AuthToken = "76d48250dec90b1c24b8964f250c3cb0"; }
                OurNumber = System.Web.Configuration.WebConfigurationManager.AppSettings["SMS_OurNumber"];
                if (OurNumber == "") { OurNumber = "+15005550006"; }
                temp = System.Web.Configuration.WebConfigurationManager.AppSettings["SendSMS_debug"];
                if (temp != "N" && Debug != "T") { Debug = temp; }
            }
            catch (Exception e)
            {
                errmsg = errmsg + "Error opening settings: " + e.ToString();
            }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\SendSMS.log";
                log4net.GlobalContext.Properties["LogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  CellNumber: " + CellNumber);
                    debuglog.Debug("  Message: " + Message + "\r\n");
                }
            }

            // Perform phone number check
            Match match = Regex.Match(CellNumber, @"\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})");
            if (match.Success)
            {
                var client = new Twilio.Clients.TwilioRestClient(AccountSid, AuthToken);
                var message = MessageResource.Create(
                    to: new PhoneNumber(CellNumber),
                    from: new PhoneNumber(OurNumber),
                    body: Message,
                    client: client);
                if (message.ErrorMessage != null)
                {
                    errmsg = message.ErrorMessage;
                    bresults = false;
                }
                else { bresults = true; }
            }
            else
            {
                bresults = false;
                errmsg = "The cell number was not a valid phone number";
            }

            // ============================================
            // Close the log file, if any
            try
            {
                DateTime et = DateTime.Now;
                if (errmsg != "")
                {
                    ltemp = "At " + et.ToString() + ", Error(s): " + errmsg + ".. for cell number " + CellNumber + ", invoked from " + callip;
                }
                else
                {
                    ltemp = "At " + et.ToString() + ", sent message to " + CellNumber + ", invoked from " + callip;
                }
                if (Debug != "T")
                {
                    if (Debug != "Y")
                    {
                        debuglog.Debug(ltemp);
                    }
                    else
                    {
                        debuglog.Debug("  Results: " + bresults + "\r\n");
                    }
                }
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "" && errmsg != "No error") { debuglog.Debug("\r\n Error: " + errmsg); }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended at " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("SendSMS Results: " + ltemp);
                if (errmsg != "" && errmsg != "No error") { eventlog.Error("SendSMS error " + errmsg); }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            return bresults;
        }

        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // Contract URLs using Bit.Ly
        [WebMethod(Description = "Perform a bit.ly contraction of a URL")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public string BitLy(string URL, string Native, string Debug)
        {
            // This webservice invokes the bitly API for the purpose of URL shortening

            // The parameters are as follows:

            //          URL     - The long URL that we want to shorten
            //          Native  - Whether to return a "native" BitLy URL or one encoded to getti.ps
            //          Debug   - A flag t o indicate the service is to be run in debug mode
            //                    "Y" - Yes for debug mode on
            //                    "N" - Yes for debuG mode off
            //                    "T" - Yes for test mode on

            // The results are returned as an XML document, similar to the following:
            //      <?xml version="1.0" encoding="utf-8"?>
            //      <string xmlns="http://cloudsvc.certegrity.com/ups/">http://ibm.co/19kJtbn</string>

            // ============================================
            // Declarations
            //  Generic        
            string errmsg = "";
            string results = "";

            // Bitly declarations
            string ApiUserName = "";
            string ApiKey = "";
            string shortened = "";

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string TestURL = "";
            string TestModeResults = "";
            string VersionNum = "101";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();

            // ============================================
            // Variable Setup
            Logging = "Y";
            Debug = Debug.ToUpper();
            if (URL.IndexOf("http://", 0) < 0 && URL.IndexOf("https://", 0) < 0) { URL = "http://" + URL; }
            if (Debug != "Y" && Debug != "T" && Debug != "N") { Debug = "N"; }
            Native = Native.ToUpper();
            if (Native != "Y" && Native != "N") { Native = "N"; }

            // ============================================
            // Get Application Keys
            //   Define API service parameters
            try
            {
                ApiUserName = System.Web.Configuration.WebConfigurationManager.AppSettings["BitLyUser"];
                if (ApiUserName == "") { ApiUserName = " hciit"; }
                ApiKey = System.Web.Configuration.WebConfigurationManager.AppSettings["BitLyKey"];
                if (ApiKey == "") { ApiKey = "R_ecd1a8e09abd73276ac1e6a51900044e"; }
                TestURL = System.Web.Configuration.WebConfigurationManager.AppSettings["BitLyTestURL"];
                if (TestURL == "") { TestURL = "http://cnn.com"; }
                TestModeResults = System.Web.Configuration.WebConfigurationManager.AppSettings["BitLyTestResults"];
                if (TestModeResults == "") { TestModeResults = "http://bit.ly/17JOagU"; }
            }
            catch (Exception e)
            {
                errmsg = errmsg + "Error opening settings: " + e.ToString();
            }
            if (Debug == "T" && TestURL != "" && TestModeResults != "") { URL = TestURL; }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\BitLy.log";
                log4net.GlobalContext.Properties["LogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  URL: " + URL);
                    debuglog.Debug("  Native: " + Native + "\r\n");
                }
            }

            // ============================================
            // Perform URL Contraction
            IBitlyService s = new BitlyService(ApiUserName, ApiKey);

            if (URL != "") { shortened = s.Shorten(URL); }

            if (Native == "N" && shortened != "") { shortened = shortened.Replace("bit.ly", "getti.ps"); }

            if (shortened != null)
            {
                results = shortened;
                if (Debug == "T")
                {
                    if (results == TestModeResults)
                    {
                        results = "Success";
                    }
                    else
                    {
                        results = "Failure";
                    }
                }
            }

            // ============================================
            // Close the log file, if any

            try
            {
                DateTime et = DateTime.Now;
                ltemp = "Results: " + results + " for " + URL + " at " + et.ToString();
                if (Debug != "Y")
                {
                    debuglog.Debug(ltemp);
                }
                else
                {
                    debuglog.Debug("Results: " + results);
                }
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "") { debuglog.Debug("\r\n Error: " + errmsg); }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended at " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("BitLy " + ltemp);
                if (errmsg != "" && errmsg != "No error") { eventlog.Error("BitLy error " + errmsg); }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            return results;
        }

        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // Generate a link for use in email
        [WebMethod(Description = "Generate a contracted link for use in email")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public string LinkMaker(string URL, string Tracking, string Contract, string Description, string TypeCd, string NoCheck, string Debug)
        {
            // This webservice invokes the bitly API for the purpose of URL shortening

            // The parameters are as follows:

            //      URL     	- The source link
            //	    Tracking 	- A flag to indicate that this link needs to be tracked
            //      Contract    - A flag to indicate that this link needs to be contracted
            //      Description - A human-readable description of the link (optional)
            //      TypeCd      - A human-readable code to categorize the link (optional)
            //      NoCheck     - A "Y"/"N" flag to indicate whether to check for duplicates or not (optional)
            //      Debug   	- A flag to indicate the service is to be run in debug mode
            //                  	  "Y" - Yes for debug mode on
            //                 	   	  "N" - Yes for debug mode off
            //                    	  "T" - Yes for test mode on

            // The results are returned as an XML document, similar to the following:
            //      <?xml version="1.0" encoding="utf-8"?>
            //      <string xmlns="http://cloudsvc.certegrity.com/ups/">http://ibm.co/19kJtbn</string>

            // ============================================
            // Declarations
            //  Generic        
            string errmsg = "";

            //  Database 
            string ConnS = "";
            string SqlS = "";
            SqlConnection con = null;
            SqlCommand cmd = null;
            SqlDataReader dr = null;

            // Service declarations
            string temp = "";
            string tURL = "";
            string tKEY = "";
            string KeyID = "";
            string RowID = "";
            string shortened = "";

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string TestURL = "";
            string TestModeResults = "";
            string VersionNum = "101";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();
            //com.certegrity.hciscormsvc.cmprofile.CMProfiles CouchBaseService = new com.certegrity.hciscormsvc.cmprofile.CMProfiles();
            com.certegrity.cloudsvc.ups.UPSServices BitLyService = new com.certegrity.cloudsvc.ups.UPSServices();

            // ============================================
            // Variable Setup
            Logging = "Y";
            Debug = Debug.ToUpper().Trim();
            Tracking = Tracking.ToUpper().Trim();
            if (Tracking != "Y" && Tracking != "N") { Tracking = "N"; }
            Contract = Contract.ToUpper().Trim();
            if (Contract != "Y" && Contract != "N") { Contract = "N"; }
            Description = Description.Trim();
            Description = Description.Replace("'", "''");
            if (Description.Length > 80) { Description.Substring(0, 80); }
            TypeCd = TypeCd.Trim();
            TypeCd = TypeCd.Replace("'", "''");
            if (TypeCd.Length > 20) { TypeCd.Substring(0, 20); }
            NoCheck = NoCheck.ToUpper().Trim();
            if (NoCheck != "Y") { NoCheck = "N"; }
            if (URL.IndexOf("http://", 0) < 0 && URL.IndexOf("https://", 0) < 0) { URL = "http://" + URL; }
            URL = URL.Trim();
            if (Debug != "Y" && Debug != "T" && Debug != "N") { Debug = "N"; }
            try
            {
                temp = WebConfigurationManager.AppSettings["LinkMaker_debug"];
                if (temp != "N" && Debug != "T") { Debug = temp; }
            }
            catch { }

            // ============================================
            // Get system defaults
            ConnectionStringSettings connSettings = ConfigurationManager.ConnectionStrings["reports"];
            if (connSettings != null)
            {
                ConnS = connSettings.ConnectionString;
            }
            if (ConnS == "")
            {
                ConnS = "server=HCIDBSQL\\HCIDB;uid=sa;pwd=k3v5c2!k3v5c2;database=reports";
            }

            // ============================================
            // Get Application Keys
            //   Define API service parameters
            try
            {
                TestURL = System.Web.Configuration.WebConfigurationManager.AppSettings["LinkMakerTestURL"];
                if (TestURL == "") { TestURL = "http://cnn.com"; }
                TestModeResults = System.Web.Configuration.WebConfigurationManager.AppSettings["LinkMakerTestResults"];
                if (TestModeResults == "") { TestModeResults = "http://bit.ly/17JOagU"; }
            }
            catch (Exception e)
            {
                errmsg = errmsg + "Error opening settings: " + e.ToString();
            }
            if (Debug == "T" && TestURL != "" && TestModeResults != "")
            {
                URL = TestURL;
                Contract = "Y";
                Tracking = "N";
            }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\LinkMaker.log";
                log4net.GlobalContext.Properties["LogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  URL: " + URL);
                    debuglog.Debug("  Description: " + Description);
                    debuglog.Debug("  TypeCd: " + TypeCd);
                    debuglog.Debug("  Tracking: " + Tracking);
                    debuglog.Debug("  NoCheck: " + NoCheck);
                    debuglog.Debug("  Contract: " + Contract + "\r\n");
                }
            }

            // ============================================
            // Open database connection
            try
            {
                errmsg = OpenDBConnection(ref ConnS, ref con, ref cmd);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
                goto CloseLog;
            }

            // ============================================
            // Process link tracking
            if (Tracking == "Y")
            {
                if (NoCheck == "N")
                {
                    // ============================================
                    // Look for and retrieve any existing URL
                    tURL = URL.ToUpper();
                    SqlS = "SELECT [KEY], ROW_ID " +
                        "FROM reports.dbo.TRACKING_LINKS " +
                        "WHERE UPPER(LINK)='" + tURL + "'";
                    if (Debug == "Y") { debuglog.Debug("Locate URL: \r\n " + SqlS); }
                    try
                    {
                        cmd = new SqlCommand(SqlS, con);
                        cmd.CommandType = System.Data.CommandType.Text;
                        dr = cmd.ExecuteReader();
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                if (dr[0] == DBNull.Value) { KeyID = ""; } else { KeyID = dr[0].ToString(); }
                                if (dr[1] == DBNull.Value) { RowID = ""; } else { RowID = dr[1].ToString(); }
                            }
                        }
                        dr.Close();
                    }
                    catch (Exception e)
                    {
                        if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                        errmsg = errmsg + "\r\nError: " + e.ToString();
                        goto CloseDB;
                    }
                }
                else
                {
                    KeyID = "";
                }

                // ============================================
                // Get the key to the link for use in tracking 
                if (KeyID != "")
                {
                    // Key was found 
                    if (Debug == "Y") { debuglog.Debug("Key was found: " + KeyID + "\r\n"); }
                    tKEY = KeyID;
                }
                // ============================================
                // Key was not found, create a new one and save the key 
                else
                {
                    if (Debug == "Y" && NoCheck != "Y") { debuglog.Debug("Key was not found, creating a new one \r\n"); }

                    // Generate a new record id
                    RowID = LoggingService.GenerateRecordId("reports.dbo.TRACKING_LINKS", "N", "N");

                    // Create the record from it
                    SqlS = "INSERT reports.dbo.TRACKING_LINKS (ROW_ID, LINK, DESCRIPTION, TYPE_CD) " +
                        "VALUES ('" + RowID + "','" + URL + "','" + Description + "','" + TypeCd + "')";
                    if (Debug == "Y") { debuglog.Debug("Insert Link Query: \r\n " + SqlS); }
                    try
                    {
                        cmd = new SqlCommand(SqlS, con);
                        cmd.CommandType = System.Data.CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                        errmsg = errmsg + "\r\nError: " + e.ToString();
                    }

                    // Locate the key for the just created link record
                    SqlS = "SELECT [KEY] " +
                        "FROM reports.dbo.TRACKING_LINKS " +
                    "WHERE ROW_ID='" + RowID + "'";
                    if (Debug == "Y") { debuglog.Debug("Locate Created URL: \r\n " + SqlS); }
                    try
                    {
                        cmd = new SqlCommand(SqlS, con);
                        cmd.CommandType = System.Data.CommandType.Text;
                        dr = cmd.ExecuteReader();
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                if (dr[0] == DBNull.Value) { KeyID = ""; } else { KeyID = dr[0].ToString(); }
                                tKEY = KeyID;
                            }
                        }
                        else
                        {
                            errmsg = "The URL was not found";
                        }
                        dr.Close();
                    }
                    catch (Exception e)
                    {
                        if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                        errmsg = errmsg + "\r\nError: " + e.ToString();
                        goto CloseDB;
                    }
                }

                // ============================================
                // Create tracking link if the link was stored correctly
                if (KeyID != "") { URL = "https://hciscorm.certegrity.com/media/LinkTracker.ashx?M=" + KeyID; }
                if (Debug == "Y")
                {
                    debuglog.Debug("KeyID: " + KeyID);
                    debuglog.Debug("tKEY: " + tKEY);
                    debuglog.Debug("New URL: " + URL);
                }
            }

            // ============================================
            // Process link contraction
            if (Contract == "Y")
            {
                shortened = BitLyService.BitLy(URL, "N", Debug);
                //shortened = shortened.Replace("https:", "http:");
                if (shortened.IndexOf("http://", 0) == 0 && URL.IndexOf("https://", 0) == 0) { shortened = shortened.Replace("http://","https://"); }
                if (Debug == "Y") { debuglog.Debug("BitLy results: " + shortened); }
            }
            else
            {
                shortened = URL;
            }

            // ============================================
            // Store the contracted link if performing tracking
            if (RowID != "")
            {
                SqlS = "UPDATE reports.dbo.TRACKING_LINKS " +
                    "SET BITLY_LINK='" + shortened + "' " +
                    "WHERE ROW_ID='" + RowID + "'";
                if (Debug == "Y") { debuglog.Debug("\r\n Update Link with BitLy Value Query: \r\n " + SqlS); }
                try
                {
                    cmd = new SqlCommand(SqlS, con);
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                    errmsg = errmsg + "\r\nError: " + e.ToString();
                }
            }

        CloseDB:
            // ============================================
            // Close database connections and objects
            try
            {
                CloseDBConnection(ref con, ref cmd, ref dr);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
            }

        CloseLog:
            // ============================================
            // Close the log file, if any
            try
            {
                DateTime et = DateTime.Now;
                ltemp = "Results: " + shortened + " for " + URL + " with Tracking:" + Tracking + ", Contract:" + Contract + ", at " + et.ToString();
                if (Debug != "Y")
                {
                    debuglog.Debug(ltemp);
                }
                else
                {
                    debuglog.Debug("Results: " + shortened);
                }
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "") { debuglog.Debug("\r\n Error: " + errmsg); }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended at " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("LinkMaker " + ltemp);
                if (errmsg != "" && errmsg != "No error") { eventlog.Error("LinkMaker error " + errmsg); }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            return shortened;
        }

        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // Geocode a Provided IP Address and store the results
        [WebMethod(Description = "Geocode a Provided IP Address")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public string GeoIP(string ipaddress, string Debug)
        {
            // This webservice uses the MaxMind GeoIP2 web service to geo-encode a provided
            // ip address and stores the results in the reports.TRACKING_IPS table

            // The parameters are as follows:

            //      ipaddress   - The Internet Protocol Version 4 (IPv4) address to locate
            //      Debug   	- A flag to indicate the service is to be run in debug mode
            //                  	  "Y" - Yes for debug mode on
            //                 	   	  "N" - Yes for debug mode off

            // ============================================
            // Declarations
            //  Generic        
            string errmsg = "";
            string temp = "";
            string existing = "";
            string Country = "";
            string State = "";
            string City = "";
            string PostalCode = "";
            string Latitude = "0.0";
            string Longitude = "0.0";

            //  Database 
            string ConnS = "";
            string SqlS = "";
            SqlConnection con = null;
            SqlCommand cmd = null;
            SqlDataReader dr = null;

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            bool GeoIpResults = false;
            string ErrLvl = "Error";
            string VersionNum = "101";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("GIPDebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();

            // ============================================
            // Variable Setup
            Logging = "Y";
            Debug = Debug.ToUpper().Trim();
            if (Debug != "Y" && Debug != "T" && Debug != "N") { Debug = "N"; }
            try
            {
                temp = WebConfigurationManager.AppSettings["GeoIP_debug"];
                if (temp != "N" && Debug != "T") { Debug = temp; }
            }
            catch { }

            // ============================================
            // Setup GeoIP Service
            int userID = 84767;
            string licenseKey = "SU0aZHITsIhf";
            string baseURL = "geoip.maxmind.com";
            int timeout = 3000;
            try
            {
                temp = WebConfigurationManager.AppSettings["GeoIP_license"];
                if (temp != "") { licenseKey = temp; }
                temp = WebConfigurationManager.AppSettings["GeoIP_userid"];
                if (temp != "") { userID = System.Convert.ToInt32(temp); }
                temp = WebConfigurationManager.AppSettings["GeoIP_baseURL"];
                if (temp != "") { baseURL = temp; }
            }
            catch { }

            // ============================================
            // Get system defaults
            ConnectionStringSettings connSettings = ConfigurationManager.ConnectionStrings["reports"];
            if (connSettings != null)
            {
                ConnS = connSettings.ConnectionString;
            }
            if (ConnS == "")
            {
                ConnS = "server=HCIDBSQL\\HCIDB;uid=sa;pwd=k3v5c2!k3v5c2;database=reports";
            }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\GeoIP.log";
                log4net.GlobalContext.Properties["GIPLogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  IP Address: " + ipaddress + "\r\n");
                }
            }

            // ============================================
            // Validate IP Address
            if (!IsValidIp(ipaddress))
            {
                errmsg = errmsg + "\r\nWarning: IP Address not valid";
                ErrLvl = "Warning";
                goto CloseLog;
            }

            // ============================================
            // Open database connections
            try
            {
                errmsg = OpenDBConnection(ref ConnS, ref con, ref cmd);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
                goto CloseLog;
            }

            // ============================================
            // See if the ip address was already processed
            if (ipaddress != "" && ipaddress.Substring(0, 7) != "192.168")
            {
                // Check to see if it isn't already in the database or not
                SqlS = "SELECT IP_ADDRESS, CITY, COUNTRY, LAT, [LONG] " +
                    "FROM reports.dbo.TRACKING_IPS " +
                    "WHERE IP_ADDRESS='" + ipaddress + "'";
                if (Debug == "Y") { debuglog.Debug("Locate existing record: \r\n " + SqlS + "\r\n"); }
                try
                {
                    cmd = new SqlCommand(SqlS, con);
                    cmd.CommandType = System.Data.CommandType.Text;
                    dr = cmd.ExecuteReader();
                    if (dr.HasRows)
                    {
                        while (dr.Read())
                        {
                            if (dr[0] == DBNull.Value) { existing = ""; } else { existing = dr[0].ToString(); }
                            if (dr[1] == DBNull.Value) { City = ""; } else { City = dr[1].ToString(); }
                            if (dr[2] == DBNull.Value) { Country = ""; } else { Country = dr[2].ToString(); }
                            if (Country == "Unknown Co") { Country = ""; }
                            if (dr[3] == DBNull.Value) { Latitude = ""; } else { Latitude = dr[3].ToString(); }
                            if (dr[4] == DBNull.Value) { Longitude = ""; } else { Longitude = dr[4].ToString(); }
                        }
                    }
                    dr.Close();
                }
                catch (Exception e)
                {
                    if (Debug == "Y") { debuglog.Debug("Database access error: " + e.ToString()); }
                    errmsg = errmsg + "\r\nError: " + e.ToString();
                    goto CloseDB;
                }

                // If an existing record was not found, geocode the provided ip address
                if (existing == "" || City == "" || Country == "")
                {
                    // Geocode ip address
                    if (existing == "" || Country == "")
                    {
                        try
                        {
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
                            var geoclient = new WebServiceClient(userID, licenseKey);
                            var geocity = geoclient.City(ipaddress);
                            Country = geocity.Country.IsoCode;
                            State = geocity.MostSpecificSubdivision.IsoCode;
                            City = geocity.City.Name;
                            PostalCode = geocity.Postal.Code;
                            Latitude = geocity.Location.Latitude.ToString();
                            Longitude = geocity.Location.Longitude.ToString();
                        }
                        catch (Exception e)
                        {
                            if (Debug == "Y") { debuglog.Debug("Unable to open MaxMind service: " + e.ToString()); }
                            errmsg = errmsg + "\r\nWarning: " + e.ToString();
                            //goto CloseDB;
                        }
                    }

                    // If no location return try alternative geocoding method
                    if (City == "" || City == null)
                    {
                        try
                        {
                            string html = geturl("http://api.hostip.info/get_html.php?ip=" + ipaddress + "&position=true", "104.27.175.109", 80, "", "");
                            if (Debug == "Y") { debuglog.Debug("From hostip: " + html); }
                            if (html != "" && html != null)
                            {
                                // parse out data here
                                if (Country == "" || Country == null) { Country = ParseHostIpResults("Country", html); }
                                if (Country == "Private Address") { Country = ""; }
                                if (Country == "UNITED STATES (US)") { Country = "US"; }
                                City = ParseHostIpResults("City", html);
                                if (City == null) { City = ""; }
                                if (City == "Private Address") { City = ""; }
                                State = ParseHostIpResults("State", html);
                                if (State == null) { State = ""; }
                                if (State == "Private Address") { State = ""; }
                                Latitude = ParseHostIpResults("Latitude", html);
                                if (Latitude == null) { Latitude = "0.0"; }
                                Longitude = ParseHostIpResults("Longitude", html);
                                if (Longitude == null) { Longitude = "0.0"; }
                            }
                        }
                        catch { }
                    }

                    // Write values found to the log
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Located values: ");
                        debuglog.Debug("  Country: " + Country);
                        debuglog.Debug("  State: " + State);
                        debuglog.Debug("  City: " + City);
                        debuglog.Debug("  PostalCode: " + PostalCode);
                        debuglog.Debug("  Latitude: " + Latitude);
                        debuglog.Debug("  Longitude: " + Longitude + "\r\n");
                    }
                }

                // Fix strings
                City = City.Replace("'", "''");
                State = State.Replace("'", "''");
                Country = Country.Replace("'", "''");
                if (City.Length > 30) { City = City.Substring(0, 30); }
                if (Country.Length > 10) { Country = Country.Substring(0, 10);  }
                if (State.Length > 10) { State = State.Substring(0, 10); }

                // Save values found to the TRACKING_IPS table
                SqlS = "";
                if (Latitude == "" || !IsNumber(Latitude)) { Latitude = "0.0"; }
                if (Longitude == "" || !IsNumber(Longitude)) { Longitude = "0.0"; }
                if (existing == "")
                {
                    SqlS = "INSERT reports.dbo.TRACKING_IPS " +
                        "(IP_ADDRESS, COUNTRY, STATE, CITY, POSTALCODE, [LAT], [LONG]) " +
                        "VALUES ('" + ipaddress + "','" + Country + "','" + State + "','" + City +
                        "','" + PostalCode + "'," + Latitude + "," + Longitude + ")";
                    if (Debug == "Y") { debuglog.Debug("Add new record: \r\n " + SqlS + "\r\n"); }
                }
                else
                {
                    if (State != "")
                    {
                        SqlS = "UPDATE reports.dbo.TRACKING_IPS " +
                            "SET COUNTRY='" + Country + "', STATE='" + State + "', CITY='" + City + "', " +
                            "POSTALCODE='" + PostalCode + "', [LAT]=" + Latitude + ", [LONG]=" + Longitude + " " +
                            "WHERE IP_ADDRESS='" + ipaddress + "'";
                        if (Debug == "Y") { debuglog.Debug("Update record: \r\n " + SqlS + "\r\n"); }
                    }
                    else
                    {
                        GeoIpResults = true;
                    }
                }
                try
                {
                    if (SqlS != "")
                    {
                        cmd = new SqlCommand(SqlS, con);
                        cmd.CommandType = System.Data.CommandType.Text;
                        cmd.ExecuteNonQuery();
                        if (Debug == "Y") { debuglog.Debug(" .. record added"); }
                        GeoIpResults = true;
                    }
                }
                catch (Exception e)
                {
                    ErrLvl = "Warning";
                    if (Debug == "Y") { debuglog.Debug("Unable to save new record: " + e.ToString()); }
                    errmsg = errmsg + "\r\n Warning: " + e.ToString();
                }
            }

        // ============================================
        // Close database connections and objects
        CloseDB:
            try
            {
                CloseDBConnection(ref con, ref cmd, ref dr);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
            }

        // ============================================
        // Close the log file, if any
        CloseLog:
            try
            {
                DateTime et = DateTime.Now;
                ltemp = "Results: " + GeoIpResults.ToString() + " for " + ipaddress + " at " + et.ToString() + ". Country = " + Country;
                if (Debug != "Y")
                {
                    debuglog.Debug(ltemp);
                }
                else
                {
                    debuglog.Debug("Results: " + GeoIpResults.ToString());
                }
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "") { debuglog.Debug("\r\n " + ErrLvl + ": " + errmsg); }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended at " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("GeoIP " + ltemp);
                if (errmsg != "" && errmsg != "No error") { eventlog.Error("GeoIP " + errmsg); }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            return GeoIpResults.ToString();
        }

        private string ParseHostIpResults(string part, string html)
        {
            // Success:		html	"Country: UNITED STATES (US)\nCity: Jay, OK\n\nLatitude: 36.4245\nLongitude: -94.7975\nIP: 75.105.128.36\n"	string
            // Failure: 	html	"Country: (Unknown Country?) (XX)\nCity: (Unknown City?)\n\nLatitude: \nLongitude: \nIP: 46.236.24.48\n"	string

            string temp = "";

            if (part == "Country")
            {
                if (html.IndexOf("(") > -1)
                {
                    try
                    {
                        temp = html.Remove(0, html.IndexOf("(") + 1);
                        temp = temp.Substring(0, temp.IndexOf(")"));
                    }
                    catch { temp = ""; }
                }
                else { temp = ""; }
                return temp;
            }

            if (part == "City")
            {
                if (html.IndexOf("City") > -1)
                {
                    try
                    {
                        temp = html.Remove(0, html.IndexOf("City:") + 6);
                        temp = temp.Substring(0, temp.IndexOf(","));
                    }
                    catch { temp = ""; }
                }
                else { temp = ""; }
                return temp;
            }

            if (part == "State")
            {
                if (html.IndexOf(",") > -1)
                {
                    try
                    {
                        temp = html.Remove(0, html.IndexOf(",") + 2);
                        temp = temp.Substring(0, temp.IndexOf("\n"));
                    }
                    catch { temp = ""; }
                }
                else { temp = ""; }
                return temp;
            }

            if (part == "Latitude")
            {
                if (html.IndexOf("Latitude:") > -1)
                {
                    try
                    {
                        temp = html.Remove(0, html.IndexOf("Latitude:") + 10);
                        temp = temp.Substring(0, temp.IndexOf("\n"));
                    }
                    catch { temp = ""; }
                }
                else { temp = "0.0"; }
                return temp;
            }

            if (part == "Longitude")
            {
                if (html.IndexOf("Longitude:") > -1)
                {
                    try
                    {
                        temp = html.Remove(0, html.IndexOf("Longitude:") + 11);
                        temp = temp.Substring(0, temp.IndexOf("\n"));
                    }
                    catch { temp = ""; }
                }
                else { temp = "0.0"; }
                return temp;
            }
            return temp;
        }
        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // Remove previously generated links
        [WebMethod(Description = "Removes the specified link")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public Boolean RemoveLink(string URL, string Id, string Debug)
        {
            // This webservice removes the keys of the specified link

            // The parameters are as follows:

            //      URL     	- The URL of the link
            //	    Id 		    - The id of the link
            //      Debug   	- A flag to indicate the service is to be run in debug mode
            //                  	  "Y" - Yes for debug mode on
            //                 	   	  "N" - Yes for debug mode off
            //                    	  "T" - Yes for test mode on

            // The results are returned as a boolean indicating success or failure

            // ============================================
            // Declarations
            //  Generic        
            string errmsg = "";

            // Service declarations
            string temp = "";
            string tURL = "";
            string RowID = "";
            string KeyID = "";

            //  Database 
            string ConnS = "";
            string SqlS = "";
            SqlConnection con = null;
            SqlCommand cmd = null;
            SqlDataReader dr = null;

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string TestURL = "";
            bool RemoveResults = false;
            string VersionNum = "101";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();

            // ============================================
            // Variable Setup
            Logging = "Y";
            Debug = Debug.ToUpper().Trim();
            if (Debug != "Y" && Debug != "T" && Debug != "N") { Debug = "N"; }
            try
            {
                temp = WebConfigurationManager.AppSettings["RemoveLink_debug"];
                if (temp != "N" && Debug != "T") { Debug = temp; }
            }
            catch { }

            // ============================================
            // Get system defaults
            ConnectionStringSettings connSettings = ConfigurationManager.ConnectionStrings["reports"];
            if (connSettings != null)
            {
                ConnS = connSettings.ConnectionString;
            }
            if (ConnS == "")
            {
                ConnS = "server=HCIDBSQL\\HCIDB;uid=sa;pwd=k3v5c2!k3v5c2;database=reports";
            }

            // ============================================
            // Get Application Keys
            //   Define API service parameters
            try
            {
                TestURL = System.Web.Configuration.WebConfigurationManager.AppSettings["RemoveLinkTestURL"];
                if (TestURL == "") { TestURL = "http://cnn.com"; }
            }
            catch (Exception e)
            {
                errmsg = errmsg + "Error opening settings: " + e.ToString();
            }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\RemoveLink.log";
                log4net.GlobalContext.Properties["LogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  URL: " + URL);
                    debuglog.Debug("  Id: " + Id + "\r\n");
                }
            }

            // ============================================
            // Open database connections
            try
            {
                errmsg = OpenDBConnection(ref ConnS, ref con, ref cmd);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
                goto CloseLog;
            }

            // ============================================
            // Locate the id key for the link if necessary
            if (URL != "" && Id == "")
            {
                // Retrieve the ID which is stored as a value to the URLs key
                if (URL.IndexOf("http://", 0) < 0) { URL = "http://" + URL; }
                tURL = URL.Trim();
                tURL = tURL.ToUpper();
                SqlS = "SELECT [KEY], ROW_ID " +
                    "FROM reports.dbo.TRACKING_LINKS " +
                    "WHERE UPPER(LINK)='" + tURL + "'";
                if (Debug == "Y") { debuglog.Debug("Locate by URL: \r\n " + SqlS); }
                try
                {
                    cmd = new SqlCommand(SqlS, con);
                    cmd.CommandType = System.Data.CommandType.Text;
                    dr = cmd.ExecuteReader();
                    if (dr.HasRows)
                    {
                        while (dr.Read())
                        {
                            if (dr[0] == DBNull.Value) { KeyID = ""; } else { KeyID = dr[0].ToString(); }
                            if (dr[1] == DBNull.Value) { RowID = ""; } else { RowID = dr[1].ToString(); }
                        }
                    }
                    dr.Close();
                }
                catch (Exception e)
                {
                    if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                    errmsg = errmsg + "\r\nError: " + e.ToString();
                    goto CloseDB;
                }
            }

            // ============================================
            // Verify key if id supplied
            if (Id != "")
            {
                SqlS = "SELECT [KEY], ROW_ID, [LINK] " +
                    "FROM reports.dbo.TRACKING_LINKS " +
                    "WHERE [KEY]=" + Id;
                if (Debug == "Y") { debuglog.Debug("Locate by Id: \r\n " + SqlS); }
                try
                {
                    cmd = new SqlCommand(SqlS, con);
                    cmd.CommandType = System.Data.CommandType.Text;
                    dr = cmd.ExecuteReader();
                    if (dr.HasRows)
                    {
                        while (dr.Read())
                        {
                            if (dr[0] == DBNull.Value) { KeyID = ""; } else { KeyID = dr[0].ToString(); }
                            if (dr[1] == DBNull.Value) { RowID = ""; } else { RowID = dr[1].ToString(); }
                            if (dr[2] == DBNull.Value) { tURL = ""; } else { tURL = dr[2].ToString(); }
                        }
                    }
                    dr.Close();
                }
                catch (Exception e)
                {
                    if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                    errmsg = errmsg + "\r\nError: " + e.ToString();
                    goto CloseDB;
                }
            }
            if (Debug == "Y")
            {
                debuglog.Debug("Keys found:");
                debuglog.Debug("KeyID: " + KeyID);
                debuglog.Debug("RowID: " + RowID + "\r\n");
            }

            // ============================================
            // Remove link found
            if (RowID != "" && KeyID != "")
            {
                SqlS = "DELETE FROM reports.dbo.TRACKING_LINKS " +
                    "WHERE [KEY]=" + KeyID;
                if (Debug == "Y") { debuglog.Debug("\r\n Remove link: \r\n " + SqlS); }
                try
                {
                    cmd = new SqlCommand(SqlS, con);
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.ExecuteNonQuery();
                    if (Debug == "Y") { debuglog.Debug(" .. link removed"); }
                    RemoveResults = true;
                }
                catch (Exception e)
                {
                    if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                    errmsg = errmsg + "\r\nError: " + e.ToString();
                }

                SqlS = "DELETE FROM reports.dbo.TRACKING_CLICKS " +
                   "WHERE [KEY]=" + KeyID;
                if (Debug == "Y") { debuglog.Debug("\r\n Remove clicks: \r\n " + SqlS); }
                try
                {
                    cmd = new SqlCommand(SqlS, con);
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.ExecuteNonQuery();
                    if (Debug == "Y") { debuglog.Debug(" .. clicks removed"); }
                }
                catch (Exception e)
                {
                    if (Debug == "Y") { debuglog.Debug("Error removing clicks: " + e.ToString()); }
                    errmsg = errmsg + "\r\nError: " + e.ToString();
                }
            }

            // ============================================
        // Close database connections and objects
        CloseDB:
            try
            {
                CloseDBConnection(ref con, ref cmd, ref dr);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
            }

            // ============================================
        // Close the log file, if any
        CloseLog:
            try
            {
                DateTime et = DateTime.Now;
                ltemp = "Results: " + RemoveResults.ToString() + " for " + tURL + " with Id:" + Id + " at " + et.ToString();
                if (Debug != "Y")
                {
                    debuglog.Debug(ltemp);
                }
                else
                {
                    debuglog.Debug("Results: " + RemoveResults.ToString());
                }
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "") { debuglog.Debug("\r\n Error: " + errmsg); }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended at " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("RemoveLink " + ltemp);
                if (errmsg != "" && errmsg != "No error") { eventlog.Error("RemoveLink error " + errmsg); }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            return RemoveResults;
        }

        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // Tracking Service
        [WebMethod(Description = "Track a UPS package")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public XmlDocument UPSTrackService(string TrackingNumber, string OutFormat, string Debug)
        {
            // This web service invokes the UPS Tracking web services API to determine
            // delivery information for a package based on the supplied Tracking Number

            // The parameters are as follows:

            //      TrackingNumber  - The UPS supplied tracking number of the package
            //      OutFormat       - The output format of the results.  If blank, "XML"
            //      Debug           - A flag to indicate the service is to be run in debug mode
            //                          "Y" - Yes for debug mode on
            //                          "N" - Yes for debug mode off
            //                          "T" - Yes for test mode on

            // The results are returned as an XML document, as follows:

            // ============================================
            // Declarations
            //  Generic        
            string mypath = "";
            string errmsg = "";
            string temp = "";
            string results = "";

            //  Database 
            //string ConnS = "";
            //string SqlS = "";
            //SqlConnection con = null;
            //SqlCommand cmd = null;
            //SqlDataReader dr = null;

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string VersionNum = "101";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();

            // UPS Authentication variables
            String UserName = "";
            String PassWord = "";
            String AccessLicense = "";

            // ============================================
            // Variable Setup
            mypath = HttpRuntime.AppDomainAppPath;
            Logging = "Y";

            // ============================================
            // Get Application Keys
            try
            {
                UserName = System.Web.Configuration.WebConfigurationManager.AppSettings["UPS_Username"];
                PassWord = System.Web.Configuration.WebConfigurationManager.AppSettings["UPS_Password"];
                AccessLicense = System.Web.Configuration.WebConfigurationManager.AppSettings["UPS_Access_Key"];
            }
            catch (Exception e)
            {
                errmsg = errmsg + "Error opening settings: " + e.ToString();
            }

            // ============================================
            // Check parameters
            if (OutFormat == "") { OutFormat = "XML"; }
            //if (OutFormat != "XML" || OutFormat != "JSON") { OutFormat = "XML"; }
            if (Debug != "T")
            {
                if (TrackingNumber == "") { errmsg = errmsg + "No Tracking Number supplied "; }
                TrackingNumber = TrackingNumber.Trim();
            }
            else
            {
                // Test package
                TrackingNumber = "1Z2944190301023433";
            }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\UPSTracking.log";
                log4net.GlobalContext.Properties["LogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  TrackingNumber: " + TrackingNumber);
                    debuglog.Debug("  OutFormat: " + OutFormat);
                }
            }

            // ============================================
            // Generate temp file name
            temp = @mypath + "temp\\" + TrackingNumber + ".xml";
            if (Debug == "Y")
            {
                debuglog.Debug("\r\nTemp filename:" + temp);
            }

            // ============================================
            // Setup complete: Track the package
            if (errmsg == "")
            {
                try
                {
                    TrackService track = new TrackService();
                    TrackRequest tr = new TrackRequest();
                    UPSSecurity upss = new UPSSecurity();
                    UPSSecurityServiceAccessToken upssSvcAccessToken = new UPSSecurityServiceAccessToken();
                    upssSvcAccessToken.AccessLicenseNumber = AccessLicense;
                    upss.ServiceAccessToken = upssSvcAccessToken;
                    UPSSecurityUsernameToken upssUsrNameToken = new UPSSecurityUsernameToken();
                    upssUsrNameToken.Username = UserName;
                    upssUsrNameToken.Password = PassWord;
                    upss.UsernameToken = upssUsrNameToken;
                    track.UPSSecurityValue = upss;
                    RequestType request = new RequestType();
                    String[] requestOption = { "15" };
                    request.RequestOption = requestOption;
                    tr.Request = request;
                    tr.InquiryNumber = TrackingNumber;
                    System.Net.ServicePointManager.CertificatePolicy = new TrustAllCertificatePolicy();
                    System.Net.ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    try
                    {
                        TrackResponse trackResponse = track.ProcessTrack(tr);
                        if (Debug == "Y")
                        {
                            debuglog.Debug("Retrieving the transaction was a " + trackResponse.Response.ResponseStatus.Description);
                            debuglog.Debug("Package was shipped " + trackResponse.Shipment[0].Service.Description);
                        }

                        // Delete the temp file
                        try { File.Delete(temp); }
                        catch { }

                        // Extract the results from the soap call and store to a local XML document.
                        //  .. To write to a file, create a StreamWriter object.
                        XmlSerializer ServiceSpy = new
                        XmlSerializer(typeof(TrackResponse));
                        StreamWriter myWriter = new StreamWriter(temp);
                        ServiceSpy.Serialize(myWriter, trackResponse);
                        myWriter.Close();

                        try
                        {
                            myWriter.Dispose();
                            myWriter = null;
                        }
                        catch (Exception ex)
                        {
                            if (Debug == "Y")
                            {
                                debuglog.Debug("\r\nGeneral Exception= " + ex.Message);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        if (Debug == "Y")
                        {
                            debuglog.Debug("\r\n >>>");
                            debuglog.Debug("  CommunicationException= " + ex.Message);
                            debuglog.Debug("  CommunicationException-StackTrace= " + ex.StackTrace);
                            debuglog.Debug("<<< \r\n");
                        }
                    }
                    
                    // ============================================
                    // Delete objects
                    try
                    {
                        track = null;
                        tr = null;
                        upss = null;
                        request = null;
                    }
                    catch (Exception ex)
                    {
                        if (Debug == "Y")
                        {
                            debuglog.Debug("\r\nGeneral Exception= " + ex.Message);
                        }
                    }
                }
                catch (System.Web.Services.Protocols.SoapException ex)
                {
                    errmsg = ex.Message;
                    if (Debug == "Y")
                    {
                        debuglog.Debug("\r\n >>>");
                        debuglog.Debug("\r\n Track Web Service returns error");
                        debuglog.Debug("  \"Hard\" is user error \"Transient\" is system error");
                        debuglog.Debug("  SoapException Message= " + ex.Message);
                        debuglog.Debug("\r\n  SoapException Category:Code:Message= " + ex.Detail.LastChild.InnerText);
                        debuglog.Debug("\r\n  SoapException XML String for all= " + ex.Detail.LastChild.OuterXml);
                        debuglog.Debug("\r\n  SoapException StackTrace= " + ex.StackTrace);
                        debuglog.Debug("<<< \r\n");
                    }
                }
                catch (System.ServiceModel.CommunicationException ex)
                {
                    errmsg = ex.Message;
                    if (Debug == "Y")
                    {
                        debuglog.Debug("\r\n >>>");
                        debuglog.Debug("  CommunicationException= " + ex.Message);
                        debuglog.Debug("  CommunicationException-StackTrace= " + ex.StackTrace);
                        debuglog.Debug("<<< \r\n");
                    }
                }
                catch (Exception ex)
                {
                    errmsg = ex.Message;
                    if (Debug == "Y")
                    {
                        debuglog.Debug("\r\n >>>");
                        debuglog.Debug("  General Exception= " + ex.Message);
                        debuglog.Debug("  General Exception-StackTrace= " + ex.StackTrace);
                        debuglog.Debug("<<< \r\n");
                    }
                }
                finally
                {
                }
            }

            // ============================================
            // Generate an XML document with the results
            if (errmsg == "") { results = "Success"; } else { results = "Failure"; }
            System.Xml.XmlDocument odoc = new System.Xml.XmlDocument();
            System.Xml.XmlDocument tdoc = new System.Xml.XmlDocument();

            // ============================================
            // Test mode output
            if (Debug == "T")
            {
                if (tdoc != null)
                {
                    using (XmlWriter writer = odoc.CreateNavigator().AppendChild())
                        try
                        {
                            writer.WriteStartDocument();
                            writer.WriteStartElement("results");
                            writer.WriteElementString("completed", results);
                            writer.WriteEndElement();
                            writer.WriteEndDocument();
                        }
                        catch (Exception e)
                        {
                            errmsg = errmsg + ", " + e.ToString();
                        }
                }
            }
            // ============================================
            // Regular output
            else
            {
                // If no error reported, then use the locally created file
                if (errmsg == "")
                {
                    // Try to load the XML document created above
                    try
                    {
                        bool nofile = false;
                        if (File.Exists(temp))
                        {
                            tdoc.Load(temp);
                            if (tdoc == null) { nofile = true; }
                        }
                        else { nofile = true; }

                        // ============================================
                        // Convert to JSON if applicable
                        if (OutFormat == "JSON" && !nofile)
                        {                            
                            string JSON = XmlToJSON(tdoc);
                            if (Debug == "Y")
                            {
                                debuglog.Debug("\r\nJSON Generated= " + JSON);
                            }

                            if (JSON != "")
                            {
                                using (XmlWriter writer = odoc.CreateNavigator().AppendChild())
                                    try
                                    {
                                        writer.WriteStartDocument();
                                        writer.WriteStartElement("results");
                                        writer.WriteElementString("json", JSON);
                                        writer.WriteEndElement();
                                        writer.WriteEndDocument();
                                    }
                                    catch (Exception e)
                                    {
                                        errmsg = errmsg + ", " + e.ToString();
                                        debuglog.Debug("\r\n >>>");
                                        debuglog.Debug("  JSON Load General Exception= " + e.Message);
                                        debuglog.Debug("  JSON Load General Exception-StackTrace= " + e.StackTrace);
                                        debuglog.Debug("<<< \r\n");
                                    }
                            }
                        }
                        else
                        {
                            if (nofile)
                            {
                                errmsg = errmsg + "Unable to loading tracking information for this number";
                            }
                            else
                            {
                                odoc = tdoc;
                            }
                        }
                        tdoc = null;
                    }
                    catch (Exception ex)
                    {
                        errmsg = ex.Message;
                        if (Debug == "Y")
                        {
                            debuglog.Debug("\r\n >>>");
                            debuglog.Debug("  JSON Load General Exception= " + ex.Message);
                            debuglog.Debug("  JSON Load General Exception-StackTrace= " + ex.StackTrace);
                            debuglog.Debug("<<< \r\n");
                        }
                    }
                }

                // ============================================
                // If an error reported, then generate an XML doc with the error
                if (errmsg != "")
                {
                    using (XmlWriter writer = odoc.CreateNavigator().AppendChild())
                        try
                        {
                            writer.WriteStartDocument();
                            writer.WriteStartElement("results");
                            writer.WriteElementString("completed", results);
                            writer.WriteElementString("errmsg", errmsg);
                            writer.WriteEndElement();
                            writer.WriteEndDocument();
                        }
                        catch (Exception e)
                        {
                            errmsg = errmsg + ", " + e.ToString();
                        }
                }
            }

            // ============================================
            // Close the log file, if any
            try
            {
                DateTime et = DateTime.Now;
                ltemp = "Results: " + results + " for Tracking # " + TrackingNumber + " at " + et.ToString();
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "") { debuglog.Debug("\r\n Error: " + errmsg); }
                    debuglog.Debug("\r\n" + ltemp);
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("UPSTrackService " + ltemp);
                if (errmsg != "" && errmsg != "No error") { eventlog.Error("UPSTrackService error " + errmsg); }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            // ============================================
            // Return Results
            return odoc;
        }

        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // Remove previously generated links
        [WebMethod(Description = "Uses Google Translate on the supplied string")]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Xml)]
        public String Translate(string SourceText, string SrcLang, string DestLang, string Debug)
        {
            // This webservice translates the string specified from the source to the 
            // destination languages specified (by 3-letter code)

            // The parameters are as follows:

            //      SourceText	- The Text to translate
            //	    SrcLang     - Source Language Code
            //	    DestLang	- Destination Language Code
            //      Debug   	- A flag to indicate the service is to be run in debug mode
            //                  	  "Y" - Yes for debug mode on
            //                 	   	  "N" - Yes for debug mode off
            //                    	  "T" - Yes for test mode on

            // The translated text is returned as a string

            // ============================================
            // Declarations
            //  Generic        
            string errmsg = "";

            // Service declarations
            string temp = "";
            string SrcLang2 = "";
            string DestLang2 = "";
            string TranslatedText = "";

            //  Database 
            string ConnS = "";
            string SqlS = "";
            SqlConnection con = null;
            SqlCommand cmd = null;
            SqlDataReader dr = null;
            CachingWrapper.LocalCache LangCache = new CachingWrapper.LocalCache();
            Data.DataTable LangTable = new Data.DataTable();

            //  Logging
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string VersionNum = "101";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            //  Web services
            com.certegrity.cloudsvc.basic.Service LoggingService = new com.certegrity.cloudsvc.basic.Service();

            // ============================================
            // Variable Setup
            Logging = "Y";
            Debug = Debug.ToUpper().Trim();
            if (Debug != "Y" && Debug != "T" && Debug != "N") { Debug = "N"; }
            try
            {
                temp = WebConfigurationManager.AppSettings["Translate_debug"];
                if (temp != "N" && Debug != "T") { Debug = temp; }
            }
            catch { }

            // Setup test transaction
            if (Debug == "T")
            {
                DestLang = "ENU";
                SourceText = "Success";
            }

            if (SrcLang == "") { SrcLang2 = "EN"; }
            SrcLang = SrcLang.ToUpper();
            
            if (DestLang == "") { goto CloseOut; }
            DestLang = DestLang.ToUpper();

            if (SourceText == "") { goto CloseOut; }
            SourceText = Server.UrlDecode(SourceText);

            // ============================================
            // Get system defaults
            ConnectionStringSettings connSettings = ConfigurationManager.ConnectionStrings["hcidb"];
            if (connSettings != null)
            {
                ConnS = connSettings.ConnectionString;
            }
            if (ConnS == "")
            {
                ConnS = "server=HCIDBSQL\\HCIDB;uid=sa;pwd=k3v5c2!k3v5c2;database=siebeldb";
            }

            // ============================================
            // Get Application Keys
            //   Define API service parameters
            //try
            //{
            //    GoogleAPIKey = System.Web.Configuration.WebConfigurationManager.AppSettings["GoogleAPIKey"];
            //    if (GoogleAPIKey == "") { GoogleAPIKey = "AIzaSyA7VtZQfJ1DTnAvqNk8hFCYIkN1uf45zxE"; }
            //}
            //catch (Exception e)
            //{
            //    errmsg = errmsg + "Error opening settings: " + e.ToString();
            //}

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\Translate.log";
                log4net.GlobalContext.Properties["LogFileName"] = logfile;
                log4net.Config.XmlConfigurator.Configure();
                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  SourceText: " + SourceText);
                    debuglog.Debug("  SrcLang: " + SrcLang);
                    debuglog.Debug("  DestLang: " + DestLang + "\r\n");
                }
            }

            // ============================================
            // Open database connections
            try
            {
                errmsg = OpenDBConnection(ref ConnS, ref con, ref cmd);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
                goto CloseLog;
            }

            // ============================================
            // Retrieve the language ids

            // See if they are in the cache
            if (LangCache.GetCachedItem("S_LANG") != null)
            {
                // Retrieve the cached table
                if (Debug == "Y") { debuglog.Debug("S_LANG found in cache: \r\n "); }
                LangTable = LangCache.GetCachedDataTable("S_LANG");
            }
            else
            {
                // Query and cache the table
                SqlS = "SELECT LANG_CD, X_CODE FROM siebeldb.dbo.S_LANG";
                if (Debug == "Y") { debuglog.Debug("Locate list of languages: \r\n " + SqlS); }
                try
                {
                    cmd = new SqlCommand(SqlS, con);
                    cmd.CommandType = System.Data.CommandType.Text;
                    dr = cmd.ExecuteReader();
                    if (dr.HasRows)
                    {
                        LangTable.Load(dr);
                        LangCache.AddToCache("S_LANG", LangTable, CachingWrapper.CachePriority.Default);
                    }
                    dr.Close();
                }
                catch (Exception e)
                {
                    if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                    errmsg = errmsg + "\r\nError: " + e.ToString();
                    goto CloseDB;
                }
            }

            // Locate Source Language Code
            if (SrcLang2 == "")
            {
                SqlS = "LANG_CD = '" + SrcLang + "'";
                if (Debug == "Y") { debuglog.Debug("Query for source language: \r\n " + SqlS); }
                DataRow[] SrcLangRec = LangTable.Select(SqlS);
                foreach (DataRow row in SrcLangRec)
                {
                    if (Debug == "Y") { debuglog.Debug("3-Letter Code: " + row[0].ToString() + ", 2-Letter Code: " + row[1].ToString() + " \r\n "); }
                    SrcLang2 = row[1].ToString().ToLower();
                }
            }

            // Locate Destination Language Code
            SqlS = "LANG_CD = '" + DestLang + "'";
            if (Debug == "Y") { debuglog.Debug("Query for destination language: \r\n " + SqlS); }
            DataRow[] DestLangRec = LangTable.Select(SqlS);
            foreach (DataRow row in DestLangRec)
            {
                if (Debug == "Y") { debuglog.Debug("3-Letter Code: " + row[0].ToString() + ", 2-Letter Code: " + row[1].ToString() + " \r\n "); }
                DestLang2 = row[1].ToString().ToLower();
            }

            // ============================================
            // Perform translation
            try
            {
                if (SourceText.Length > 1000)
                {
                    var regex = new Regex(@"(.{1,1000})(?:\s|$)");
                    var SourceTextList = regex.Matches(SourceText)
                                               .Cast<Match>()
                                               .Select(m => m.Groups[1].Value)
                                               .ToList();
                    foreach (string SourceLine in SourceTextList)
                    {
                        if (Debug == "Y") { debuglog.Debug("SourceLine: " + SourceLine); }
                        KeyValuePair<string, string> translation = TranslationManager.Instance.Translate(SourceLine, DestLang2);
                        TranslatedText = TranslatedText + translation.Key.ToString();
                    }
                }
                else
                {
                    KeyValuePair<string, string> translation = TranslationManager.Instance.Translate(SourceText, DestLang2);
                    TranslatedText = translation.Key.ToString();
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Translated: " + translation.Key.ToString());
                        debuglog.Debug("From language: " + translation.Value.ToString() + "\r\n");
                    }
                }

            }
            catch (Exception e)
            {
                if (Debug == "Y") { debuglog.Debug("Error: " + e.ToString()); }
                errmsg = errmsg + "\r\nError: " + e.ToString();
            }

        // ============================================
        // Close database connections and objects
        CloseDB:
            try
            {
                CloseDBConnection(ref con, ref cmd, ref dr);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
            }

            // ============================================
        // Close the log file, if any
        CloseLog:
            try
            {
                DateTime et = DateTime.Now;
                ltemp = "Results: '" + TranslatedText + "' at " + et.ToString();
                if (Debug != "Y")
                {
                    debuglog.Debug(ltemp);
                }
                else
                {
                    debuglog.Debug("\r\nResults: " + TranslatedText);
                }
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    if (errmsg != "") { debuglog.Debug("\r\n Error: " + errmsg); }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended at " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
                eventlog.Info("Translate " + ltemp);
                if (errmsg != "" && errmsg != "No error") { 
                    eventlog.Error("Translate error " + errmsg);
                    if (Debug == "T") { TranslatedText = "Failure"; }
                }
            }
            catch
            {
            }

            // ============================================
            // Log Performance Data
        CloseOut:
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                String MyMethod = System.Reflection.MethodBase.GetCurrentMethod().Name;
                LoggingService.LogPerformanceData2Async(MyMachine, MyMethod, LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            return TranslatedText;
        }

        //  ////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ============================================
        // STRING FUNCTIONS
        public Boolean IsNumber(object Expression)
        {
            double retNum;
            bool isNum = Double.TryParse(Convert.ToString(Expression), System.Globalization.NumberStyles.Any, System.Globalization.NumberFormatInfo.InvariantInfo, out retNum);
            return isNum;
        }

        // ============================================
        // SERVICE FUNCTIONS
        private XmlReader getServiceResult(string serviceUrl)
        {
            HttpWebRequest HttpWReq;
            HttpWebResponse HttpWResp;
            HttpWReq = (HttpWebRequest)WebRequest.Create(serviceUrl);
            HttpWReq.Method = "GET";
            HttpWResp = (HttpWebResponse)HttpWReq.GetResponse();
            if (HttpWResp.StatusCode == HttpStatusCode.OK)
            {
                //Consume webservice with basic XML reading, assumes it returns (one) string
                XmlReader reader = XmlReader.Create(HttpWResp.GetResponseStream());
                return reader;
            }
            else
            {
                throw new Exception("Error on remote IP to Country service: " + HttpWResp.StatusCode.ToString());
            }
        }

        public string geturl(string url, string proxyip, int port, string proxylogin, string proxypassword)
        {
            HttpWebResponse resp;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0";
            req.AllowAutoRedirect = true;
            req.ReadWriteTimeout = 5000;
            req.CookieContainer = new CookieContainer();
            req.Referer = "";
            req.Headers.Set("Accept-Language", "en,en-us");
            StreamReader stream_in;

            WebProxy proxy = new WebProxy(proxyip, port);
            //if proxylogin is an empty string then don't use proxy credentials (open proxy)
            if (proxylogin != "") proxy.Credentials = new NetworkCredential(proxylogin, proxypassword);
            req.Proxy = proxy;

            string response = "";
            try
            {
                resp = (HttpWebResponse)req.GetResponse();
                stream_in = new StreamReader(resp.GetResponseStream());
                response = stream_in.ReadToEnd();
                stream_in.Close();
            }
            catch (Exception ex)
            {
            }
            return response;
        }

        // ============================================
        // DATABASE FUNCTIONS
        // Open Database Connection
        private string OpenDBConnection(ref string ConnS, ref SqlConnection con, ref SqlCommand cmd)
        {
            string SqlS = "";
            string result = "";
            try
            {
                con = new SqlConnection(ConnS);
                con.Open();
                if (con != null)
                {
                    try
                    {
                        cmd = new SqlCommand(SqlS, con);
                        cmd.CommandTimeout = 300;
                    }
                    catch (Exception ex2) { result = "Open error: " + ex2.ToString(); }
                }
            }
            catch
            {
                if (con.State != System.Data.ConnectionState.Closed) { con.Dispose(); }
                ConnS = ConnS + ";Pooling=false";
                try
                {
                    con = new SqlConnection(ConnS);
                    con.Open();
                    if (con != null)
                    {
                        try
                        {
                            cmd = new SqlCommand(SqlS, con);
                            cmd.CommandTimeout = 300;
                        }
                        catch (Exception ex2)
                        {
                            result = "Open error: " + ex2.ToString();
                        }
                    }
                }
                catch (Exception ex2)
                {
                    result = "Open error: " + ex2.ToString();
                }
            }
            return result;
        }

        // Close Database Connection
        private void CloseDBConnection(ref SqlConnection con, ref SqlCommand cmd, ref SqlDataReader dr)
        {
            // This function closes a database connection safely

            // Handle datareader
            try
            {
                dr.Close();
            }
            catch { }

            try
            {
                dr = null;
            }
            catch { }


            // Handle command
            try
            {
                cmd.Dispose();
            }
            catch { }

            try
            {
                cmd = null;
            }
            catch { }


            // Handle connection
            try
            {
                con.Close();
            }
            catch { }

            try
            {
                SqlConnection.ClearPool(con);
            }
            catch { }

            try
            {
                con.Dispose();
            }
            catch { }

            try
            {
                con = null;
            }
            catch { }
        }

        // Convert Object to Datatable
        #region Converting ObjectArray to Datatable
        private Data.DataTable ConvertToDataTable(Object[] array)
        {
            PropertyInfo[] properties = array.GetType().GetElementType().GetProperties();
            Data.DataTable dt = CreateDataTable(properties);
            if (array.Length != 0)
            {
                foreach (object o in array)
                    FillData(properties, dt, o);
            }
            return dt;
        }

        private DataTable CreateDataTable(PropertyInfo[] properties)
        {
            DataTable dt = new DataTable();
            DataColumn dc = null;
            foreach (PropertyInfo pi in properties)
            {
                dc = new DataColumn();
                dc.ColumnName = pi.Name;
                dc.DataType = pi.PropertyType;
                dt.Columns.Add(dc);
            }
            return dt;
        }

        private void FillData(PropertyInfo[] properties, DataTable dt, Object o)
        {
            DataRow dr = dt.NewRow();
            foreach (PropertyInfo pi in properties)
            {
                dr[pi.Name] = pi.GetValue(o, null);
            }
            dt.Rows.Add(dr);
        }

        #endregion

        // ============================================
        // XML TO JSON CONVERTER
        private static string XmlToJSON(XmlDocument xmlDoc)
        {
            StringBuilder sbJSON = new StringBuilder();
            sbJSON.Append("{ ");
            XmlToJSONnode(sbJSON, xmlDoc.DocumentElement, true);
            sbJSON.Append("}");
            return sbJSON.ToString();
        }

        //  XmlToJSONnode:  Output an XmlElement, possibly as part of a higher array
        private static void XmlToJSONnode(StringBuilder sbJSON, XmlElement node, bool showNodeName)
        {
            if (showNodeName)
                sbJSON.Append("\"" + SafeJSON(node.Name) + "\": ");
            sbJSON.Append("{");
            // Build a sorted list of key-value pairs
            //  where   key is case-sensitive nodeName
            //          value is an ArrayList of string or XmlElement
            //  so that we know whether the nodeName is an array or not.
            SortedList childNodeNames = new System.Collections.SortedList();

            //  Add in all node attributes
            if (node.Attributes != null)
                foreach (XmlAttribute attr in node.Attributes)
                    StoreChildNode(childNodeNames, attr.Name, attr.InnerText);

            //  Add in all nodes
            foreach (XmlNode cnode in node.ChildNodes)
            {
                if (cnode is XmlText)
                    StoreChildNode(childNodeNames, "value", cnode.InnerText);
                else if (cnode is XmlElement)
                    StoreChildNode(childNodeNames, cnode.Name, cnode);
            }

            // Now output all stored info
            foreach (string childname in childNodeNames.Keys)
            {
                ArrayList alChild = (ArrayList)childNodeNames[childname];
                if (alChild.Count == 1)
                    OutputNode(childname, alChild[0], sbJSON, true);
                else
                {
                    sbJSON.Append(" \"" + SafeJSON(childname) + "\": [ ");
                    foreach (object Child in alChild)
                        OutputNode(childname, Child, sbJSON, false);
                    sbJSON.Remove(sbJSON.Length - 2, 2);
                    sbJSON.Append(" ], ");
                }
            }
            sbJSON.Remove(sbJSON.Length - 2, 2);
            sbJSON.Append(" }");
        }

        //  StoreChildNode: Store data associated with each nodeName
        //                  so that we know whether the nodeName is an array or not.
        private static void StoreChildNode(SortedList childNodeNames, string nodeName, object nodeValue)
        {
            // Pre-process contraction of XmlElement-s
            if (nodeValue is XmlElement)
            {
                // Convert  <aa></aa> into "aa":null
                //          <aa>xx</aa> into "aa":"xx"
                XmlNode cnode = (XmlNode)nodeValue;
                if (cnode.Attributes.Count == 0)
                {
                    XmlNodeList children = cnode.ChildNodes;
                    if (children.Count == 0)
                        nodeValue = null;
                    else if (children.Count == 1 && (children[0] is XmlText))
                        nodeValue = ((XmlText)(children[0])).InnerText;
                }
            }
            // Add nodeValue to ArrayList associated with each nodeName
            // If nodeName doesn't exist then add it
            object oValuesAL = childNodeNames[nodeName];
            ArrayList ValuesAL;
            if (oValuesAL == null)
            {
                ValuesAL = new ArrayList();
                childNodeNames[nodeName] = ValuesAL;
            }
            else
                ValuesAL = (ArrayList)oValuesAL;
            ValuesAL.Add(nodeValue);
        }

        private static void OutputNode(string childname, object alChild, StringBuilder sbJSON, bool showNodeName)
        {
            if (alChild == null)
            {
                if (showNodeName)
                    sbJSON.Append("\"" + SafeJSON(childname) + "\": ");
                sbJSON.Append("null");
            }
            else if (alChild is string)
            {
                if (showNodeName)
                    sbJSON.Append("\"" + SafeJSON(childname) + "\": ");
                string sChild = (string)alChild;
                sChild = sChild.Trim();
                sbJSON.Append("\"" + SafeJSON(sChild) + "\"");
            }
            else
                XmlToJSONnode(sbJSON, (XmlElement)alChild, showNodeName);
            sbJSON.Append(", ");
        }

        // Make a string safe for JSON
        private static string SafeJSON(string sIn)
        {
            StringBuilder sbOut = new StringBuilder(sIn.Length);
            foreach (char ch in sIn)
            {
                if (Char.IsControl(ch) || ch == '\'')
                {
                    int ich = (int)ch;
                    sbOut.Append(@"\u" + ich.ToString("x4"));
                    continue;
                }
                else if (ch == '\"' || ch == '\\' || ch == '/')
                {
                    sbOut.Append('\\');
                }
                sbOut.Append(ch);
            }
            return sbOut.ToString();
        }

        // ============================================
        // DATA VALIDATION FUNCTIONS
        public bool IsValidIp(string addr)
        {
            var quads = addr.Split('.');

            // if we do not have 4 quads, return false
            if (!(quads.Length == 4)) return false;

            // for each quad
            foreach (var quad in quads)
            {
                int q;
                // if parse fails 
                // or length of parsed int != length of quad string (i.e.; '1' vs '001')
                // or parsed int < 0
                // or parsed int > 255
                // return false
                if (!Int32.TryParse(quad, out q)
                    || !q.ToString().Length.Equals(quad.Length)
                    || q < 0
                    || q > 255) { return false; }

            }

            return true;
        }

        // ============================================
        // DEBUG FUNCTIONS
        private bool writeoutputfs(ref FileStream fs, String instring)
        {
            // This function writes a line to a previously opened filestream, and then flushes it
            // promptly.  This assists in debugging services
            Boolean result;
            try
            {
                instring = instring + "\r\n";
                //byte[] bytesStream = new byte[instring.Length];
                Byte[] bytes = encoding.GetBytes(instring);
                fs.Write(bytes, 0, bytes.Length);
                result = true;
            }
            catch
            {
                result = false;
            }
            fs.Flush();
            return result;
        }
    }

    // ============================================
    // Class for performing Google Translations
    public sealed class TranslationManager
    {
        private static TranslationManager mInstance = null;
        private static object mSyncObj = new object();

        public static TranslationManager Instance
        {
            get
            {
                if (mInstance == null)
                {
                    lock (mSyncObj)
                    {
                        mInstance = new TranslationManager();
                    }
                }
                return mInstance;
            }
        }

        private TranslationManager()
        {

        }

        private string GetApiKey()
        {
            string GoogleAPIKey = "";
            GoogleAPIKey = System.Web.Configuration.WebConfigurationManager.AppSettings["GoogleAPIKey"];
            if (GoogleAPIKey == "") { GoogleAPIKey = "AIzaSyB6yoKJuoFUkMMdcWEh2GamWJT4GIJiTig"; }
            return GoogleAPIKey;
        }

        public KeyValuePair<string, string> Translate(string srcText, string target_language = "en")
        {
            string[] text = new string[1] { srcText };
            Dictionary<string, KeyValuePair<string, string>> translation = Translate(text, target_language);
            return translation[srcText];
        }

        public Dictionary<string, KeyValuePair<string, string>> Translate(string[] srcText, string target_language = "en")
        {

            // Create the service.
            var service = new TranslateService(new BaseClientService.Initializer()
            {
                ApiKey = GetApiKey(),
                ApplicationName = "Spanish"
            });

            TranslationsListResponse response = service.Translations.List(srcText, target_language).Execute();
            Dictionary<string, KeyValuePair<string, string>> translations = new Dictionary<string, KeyValuePair<string, string>>();

            int counter = 0;
            foreach (TranslationsResource translation in response.Translations)
            {
                translations[srcText[counter]] = new KeyValuePair<string, string>(translation.TranslatedText, translation.DetectedSourceLanguage);
                counter++;
            }

            return translations;
        }
    }
}