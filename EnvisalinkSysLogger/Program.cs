// See https://aka.ms/new-console-template for more information
using System.Globalization;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;


Console.WriteLine("Hello, World!");
var app = new App();
string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
string datafiles = $@"{exeDirectory}\..\..\..\..\..\Envisalink-Syslogger-data";
app.zonesfilePath = $@"{datafiles}\zones.csv";
app.reportPrefix = $@"{datafiles}\Envisalog";
app.reportFile = app.reportPrefix + ".html"; ;
app.outfile = $@"{datafiles}\log.csv";
app.appname = "ENVISALINK";
app.serviceAccountKeyFilePath = $@"{datafiles}\service-account-key.json";
app.folderId = File.ReadAllText(datafiles + @"\folderid.txt").Trim();
app.Start();
Console.WriteLine("Hello, World!");


class App
{
    public string folderId = "";
    public string serviceAccountKeyFilePath = "";
    public int SyslogPort = 514;
    public string zonesfilePath = "";
    public string reportPrefix = "";
    public string reportFile = "";
    public string outfile = "";
    public string appname = "";
    Dictionary<string, string> zonesDictionary;
    string[] Scopes = { DriveService.Scope.DriveFile };
    DriveService service = null;
    DateTime LastUpload = DateTime.Now.AddMinutes(-11);

    public void Start()
    {

        try
        {
            service = DriveSetup();
        }
        catch (Exception ex2)
        {
            Console.WriteLine($"An error occurred (Google Drive setup): {ex2.ToString()}");
        }

        //// debugging
        //Console.WriteLine("COMMENT this out before publish (right click of solution) and restart (shell:startup)");
        //Console.ReadLine();
        //CreateReport(outfile, reportFile);
        //Environment.Exit(1);

        UdpClient udpClient;
        udpClient = new UdpClient(SyslogPort);

        Console.WriteLine($"Syslog server started on port {SyslogPort}.");
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, SyslogPort);
        zonesDictionary = ReadZonesFromFile(zonesfilePath);
        Console.WriteLine("Ready.");
        try
        {
            while (true)
            {
                byte[] receiveBytes = udpClient.Receive(ref remoteEndPoint);
                string syslogMessage = Encoding.ASCII.GetString(receiveBytes);

                try
                {
                    ProcessMessage(syslogMessage);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"An error occurred: {ex2.ToString()}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.ToString()}");
        }
        finally
        {
            udpClient.Close();
        }
    }

    void ProcessMessage(string syslogMessage)
    {
        int strpos;
        if (syslogMessage != null && (strpos = syslogMessage.LastIndexOf(appname)) >= 0)
        {
            var color = "FFFFFF,000000";
            var formattedMessage = syslogMessage.Substring(strpos + appname.Length);
            if ((strpos = syslogMessage.IndexOf("]: ")) >= 0)
            {
                formattedMessage = syslogMessage.Substring(strpos + 2);
            }
            string formattedDate = DateTime.Now.ToString("yyyy/MM/dd ddd hh:mm:ss tt");
            var finalmessage = $"{formattedDate},{formattedMessage.Trim()}";
            if ((strpos = finalmessage.IndexOf("Zone")) >= 0)
            {
                var spl = finalmessage.Split(' ');
                if (zonesDictionary.TryGetValue(spl[spl.Length - 1], out var zonelabel))
                {
                    spl = zonelabel.Split(',');
                    zonelabel = spl[0];
                    finalmessage += " " + zonelabel;
                    if (spl.Length > 2 && !formattedMessage.Contains("Close"))
                    {
                        color = spl[1] + "," + spl[2];
                    }
                }
            }
            finalmessage = $"{color},{finalmessage}";
            File.AppendAllText(outfile, finalmessage + "\r\n");
            CreateReport(outfile, reportFile);
            Console.WriteLine(finalmessage);

            try
            {
                if (DateTime.Now.Subtract(LastUpload).TotalMinutes > 10)
                {
                    LastUpload = DateTime.Now;
                    UploadFile(service, reportFile, "text/html");
                    Console.WriteLine($"Uploaded file");
                }
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Can't upload file: {ex2.ToString()}");
            }

        }
    }

    Dictionary<string, string> ReadZonesFromFile(string filePath)
    {
        var zonesDictionary = new Dictionary<string, string>();

        using (var reader = new StreamReader(filePath))
        {
            // Read the header line
            var headerLine = reader.ReadLine();

            // Read each subsequent line
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');

                if (values.Length > 1)
                {
                    Console.WriteLine($"Processing line from csv: '{line}'");
                    string zone = values[0].Trim();
                    string label = (line + " ").Substring(line.IndexOf(",") + 1).Trim();
                    zonesDictionary.Add(zone, label);
                }
                else
                {
                    Console.WriteLine($"Not processing line from csv: '{line}'");
                }
            }
        }

        return zonesDictionary;

    }


    DriveService DriveSetup()
    {
        // Path to the service account key file
        
        // The file to upload

        // Load service account credentials
        GoogleCredential credential;
        using (var stream = new FileStream(serviceAccountKeyFilePath, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(DriveService.Scope.DriveFile);
        }

        // Create the Drive API service
        var service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Google Drive API .NET Quickstart",
        });
        return service;

    }

    void UploadFile(DriveService service, string filePath, string mimeType)
    {
        string fileName = Path.GetFileName(filePath);

        // Set scopes for google drive api: ../auth/drive and ../auth/drive.readonly. 
        // Search for an existing file with the same name in the specified folder
        var searchRequest = service.Files.List();
        searchRequest.Q = $"name contains 'Envisal' and '{folderId}' in parents and trashed = false";
        searchRequest.Fields = "files(id, name)";
        var searchResult = searchRequest.Execute();

        // If files with the same prefix exist, delete them
        foreach (var myfile in searchResult.Files)
        {
            // add as editor in IAM (grant acces): eyesonsysloggerserviceacct@fastal-test-project.iam.gserviceaccount.com EyesOnSysLoggerServiceAcct             Editor
            service.Files.Delete(myfile.Id).Execute();
            Console.WriteLine("Deleted: " + myfile.Id);
        }

        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = Path.GetFileName(filePath),
            Parents = new List<string> { folderId }
        };
        FilesResource.CreateMediaUpload request;
        using (var stream = new FileStream(filePath, FileMode.Open))
        {
            request = service.Files.Create(fileMetadata, stream, mimeType);
            request.Fields = "id";
            request.Upload();
        }
        var file = request.ResponseBody;
        Console.WriteLine("File ID: " + file.Id);
    }



    void CreateReport(string csvFilePath, string htmlFilePath)
    {
        // prompt: Sure! Here is the updated C# program that reads the CSV file, filters the entries from the last 24 hours, generates an HTML file with a modern, mobile-friendly table, hides the color code columns, adds an extra column for row numbers, and sorts by row number when the date header is clicked
        // Read CSV file
        var csvLines = File.ReadAllLines(csvFilePath);

        // Parse CSV lines into a list of Event objects
        var events = new List<Event>();
        for (int i = 1; i < csvLines.Length; i++) // Skip header line
        {
            var columns = csvLines[i].Split(',');
            if (columns.Length == 4)
            {
                if (DateTime.TryParseExact(columns[2], "yyyy/MM/dd ddd hh:mm:ss tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime eventDate))
                {
                    events.Add(new Event
                    {
                        RowNumber = i,
                        Backcolor = columns[0],
                        Forecolor = columns[1],
                        EventDate = eventDate,
                        EventDetails = columns[3]
                    });
                }
            }
        }

        // Filter events from the last 24 hours
        var now = DateTime.Now;
        var oneDayAgo = now.AddHours(-24);
        // only zone opens
        events = events.Where((e) => e.EventDate >= oneDayAgo
                                             && e.EventDate <= now
                                             && e.EventDetails.ToLower().Contains("zone open:")
                                           ).ToList();

        var events2 = new List<Event>();
        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (i >= 2
                && ev.EventDetails == events[i - 2].EventDetails
                && ev.EventDetails == events[i - 1].EventDetails)
            {
                var last = events2.Last();
                var timestr = ev.EventDate.ToString("HH:mm:ss");

                if (last.EventDetails.Contains("Zone Open"))
                {
                    last.EventDetails = last.EventDate.ToString("HH:mm:ss") + "; " + timestr;
                }
                else if (last.EventDetails.Length > 25)
                {
                    if (!last.EventDetails.Contains("..."))
                    {
                        last.EventDetails += " ...";
                    }
                }
                else
                {
                    last.EventDetails += "; " + timestr;
                }
                last.EventDate = ev.EventDate;
            }
            else
            {
                var json = JsonSerializer.Serialize(ev);
                events2.Add(JsonSerializer.Deserialize<Event>(json));
            }

        }

        // get rid of 'zone open' since they all are.
        events2.ForEach(e => e.EventDetails =
                         e.EventDetails.Replace("Zone Open: ", "", StringComparison.CurrentCultureIgnoreCase));
        // Generate HTML
        var htmlBuilder = new StringBuilder();
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang=\"en\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
        htmlBuilder.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        htmlBuilder.AppendLine("    <title>Event Logs Table</title>");
        htmlBuilder.AppendLine("    <style>");
        htmlBuilder.AppendLine("        body {");
        htmlBuilder.AppendLine("            font-family: Arial, sans-serif;");
        htmlBuilder.AppendLine("            margin: 0;");
        htmlBuilder.AppendLine("            padding: 0;");
        htmlBuilder.AppendLine("            justify-content: center;");
        htmlBuilder.AppendLine("        }");
        htmlBuilder.AppendLine("        table {");
        htmlBuilder.AppendLine("            width: 90%;");
        htmlBuilder.AppendLine("            max-width: 800px;");
        htmlBuilder.AppendLine("            overflow-y: auto;");
        htmlBuilder.AppendLine("            border-collapse: collapse;");
        htmlBuilder.AppendLine("            box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);");
        htmlBuilder.AppendLine("            margin: 0 auto;");
        htmlBuilder.AppendLine("        }");
        htmlBuilder.AppendLine("        th, td {");
        htmlBuilder.AppendLine("            padding: 12px 15px;");
        htmlBuilder.AppendLine("            border: 1px solid #ddd;");
        htmlBuilder.AppendLine("            text-align: left;");
        htmlBuilder.AppendLine("        }");
        htmlBuilder.AppendLine("        th {");
        htmlBuilder.AppendLine("            background-color: #f4f4f4;");
        htmlBuilder.AppendLine("            top: 0;");
        htmlBuilder.AppendLine("            z-index: 1;");
        htmlBuilder.AppendLine("        }");
        htmlBuilder.AppendLine("    </style>");
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        htmlBuilder.AppendLine("    <table id=\"eventTable\">");
        htmlBuilder.AppendLine("        <thead>");
        htmlBuilder.AppendLine("            <tr>");
        htmlBuilder.AppendLine("                <th >Event Date</th>");
        htmlBuilder.AppendLine("                <th >Event Details</th>");
        htmlBuilder.AppendLine("            </tr>");
        htmlBuilder.AppendLine("        </thead>");
        htmlBuilder.AppendLine("        <tbody>");

        foreach (var ev in events2)
        {
            var tm = ev.EventDate.ToString("ddd HH:mm:ss").Replace(" ", "&nbsp");
            htmlBuilder.AppendLine("             <tr>");
            htmlBuilder.AppendLine($"                 <td style=\"background-color: {ev.Backcolor}; color: {ev.Forecolor};\">{tm}</td>");
            htmlBuilder.AppendLine($"                 <td style=\"background-color: {ev.Backcolor}; color: {ev.Forecolor};\">{ev.EventDetails.Replace(" ", "&nbsp")}</td>");
            htmlBuilder.AppendLine("             </tr>");
        }
        htmlBuilder.AppendLine("        </tbody>");
        htmlBuilder.AppendLine("    </table>");
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");

        // Write HTML to file
        File.WriteAllText(htmlFilePath, htmlBuilder.ToString());
    }
}

class Event
{
    public int RowNumber { get; set; }
    public string Backcolor { get; set; }
    public string Forecolor { get; set; }
    public DateTime EventDate { get; set; }
    public string EventDetails { get; set; }
}