using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AirtableApiClient;
using autoblog.Model;
using Newtonsoft.Json.Linq;

namespace autoblog
{
  class Program
  {

    readonly static string baseId = Environment.GetEnvironmentVariable("baseId");
    readonly static string appKey = Environment.GetEnvironmentVariable("appKey");

    static List<CDA> CDATeam { get; set; }
    static List<Technology> Technologies { get; set; }

    // static List<ContentType> ContentTypes { get; set; }
    static List<Topic> Topics { get; set; }

    static List<ContentItem> ContentItems { get; set; }

    static async Task Main(string[] args)
    {

      var root = $"/app/{Environment.GetEnvironmentVariable("dir")}";

      if (Debugger.IsAttached)
      {
        root = Environment.GetEnvironmentVariable("dir");
      }

      if (!Directory.Exists(root))
      {
        Directory.CreateDirectory(root);
      }

      var output = new System.Text.StringBuilder();
      output.AppendLine(Bash($"git -C {root} pull"));

      await GetAirTableTeamDataAsync();
      // await GetContentTypesDataAsync();
      await GetTopicsDataAsync();
      await GetTechnologiesDataAsync();

      await GetContentRepositoryItemsAsync();

      var creator = new Creator.Post(ContentItems);
      try
      {
        var fileContents = await creator.Create();
        await File.WriteAllTextAsync(Path.Join("_posts", creator.FileName), fileContents);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
        throw ex;
      }

      output.AppendLine(Bash($"git add ."));
      output.AppendLine(Bash($"git commit -m '{creator.FileName} added'"));
      output.AppendLine(Bash("git push"));

      Console.WriteLine(output.ToString());
    }

    private static string UpdateRepo()
    {
      var result = new StringBuilder().AppendLine(Bash("git checkout master"));
      result.AppendLine(Bash("git pull"));

      return result.ToString();
    }

    private static async Task<List<AirtableRecord>> GetAirTableDataAsync(string table, List<string> fields, string view = null)
    {
      string offset = null;
      string errorMessage = null;
      var records = new List<AirtableRecord>();

      using (AirtableBase airtableBase = new AirtableBase(appKey, baseId))
      {
        //
        // Use 'offset' and 'pageSize' to specify the records that you want
        // to retrieve.
        // Only use a 'do while' loop if you want to get multiple pages
        // of records.
        //

        do
        {
          Task<AirtableListRecordsResponse> task = airtableBase.ListRecords(
                 table, offset, fields, null, null, null, null, view);

          AirtableListRecordsResponse response = await task;

          if (response.Success)
          {
            records.AddRange(response.Records.ToList());
            offset = response.Offset;
          }
          else if (response.AirtableApiError is AirtableApiException)
          {
            errorMessage = response.AirtableApiError.ErrorMessage;
            break;
          }
          else
          {
            errorMessage = "Unknown error";
            break;
          }
        } while (offset != null);
      }

      if (!string.IsNullOrEmpty(errorMessage))
      {
        // Error reporting
        throw new Exception(errorMessage);
      }
      else
      {
        // Do something with the retrieved 'records' and the 'offset'
        // for the next page of the record list.
        return records;
      }
    }

    private static async Task GetContentRepositoryItemsAsync()
    {
      var fields = new List<string>() { "Title", "CA Name", "Target Completion Date", "Topic Areas", "Technologies", "Category", "Description", "Links", "MS CTA Links" };
      var records = await GetAirTableDataAsync("Content Repository", fields, "Weekly Roundup - Social + PMM");


      ContentItems = (records.Select(r => new ContentItem()
      {
        Title = (string)r.GetField("Title"),
        Id = r.Id,
        CDAName = GetCDAName(r.GetField("CA Name")),
        TargetCompletionDate = GetTargetCompletionDate(r.GetField("Target Completion Date")),
        Technologies = GetContentItemTechnologies(r.GetField("Technologies")),
        Description = (string)r.GetField("Description"),
        Links = GetContentItemLinks(r.GetField("Links")),
        CTALinks = GetContentItemLinks(r.GetField("MS CTA Links"))
      })).ToList();

    }

    private static List<string> GetContentItemLinks(object value)
    {
      if (value != null)
      {
        var results = new List<string>();
        results.AddRange(SplitOnKnownChars((string)value));

        // string contcatenated by ' and '
        //var items = ((string)value).Split(" and ", StringSplitOptions.RemoveEmptyEntries).ToList();

        // foreach (var link in items)
        // {
        //   if (link.Contains("\n"))
        //   {
        //     var linkItems = link.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        //     results.AddRange(linkItems);
        //   }
        //   else if (link.Contains(","))
        //   {
        //     var linkItems = link.Split(",", StringSplitOptions.RemoveEmptyEntries);
        //     results.AddRange(linkItems);
        //   }
        //   else if (link.Contains(";"))
        //   {
        //     var linkItems = link.Split(";", StringSplitOptions.RemoveEmptyEntries);
        //     results.AddRange(linkItems);
        //   }
        //   else
        //   {
        //     // check for "nothing" values
        //
        //   }
        // }

        return results;
      }
      return new List<string>();
    }

    private static HashSet<string> SplitOnKnownChars(string link)
    {
      string[] chars = { "\n", "&", "," };

      var results = new HashSet<string>();

      Array.ForEach(chars, c =>
      {
        var temp = link.Split(c, StringSplitOptions.RemoveEmptyEntries);
        Array.ForEach(temp, t =>
        {
          if (!string.IsNullOrEmpty(t) &&
                  !string.IsNullOrWhiteSpace(t) &&
                  !string.Equals("na", t, StringComparison.CurrentCultureIgnoreCase) &&
                  !string.Equals("n/a", t, StringComparison.CurrentCultureIgnoreCase) &&
                  !string.Equals("-", t, StringComparison.CurrentCultureIgnoreCase)
                  )
          {
            results.Add(t.Trim());
          }
        });
      });

      return results;
    }

    private static List<string> GetContentItemTechnologies(object value)
    {
      if (value != null)
      {
        var items = FieldToList(value);
        var result = from t in Technologies
                     where items.Contains(t.Id)
                     select t.Name;

        return result.ToList();
      }
      return new List<string>();
    }
    private static string GetTargetCompletionDate(object value)
    {
      if (value.GetType() == typeof(string))
      {
        return Convert.ToDateTime(value).ToShortDateString();
      }
      else
      {
        var items = FieldToList(value);
        if (items.Count > 0)
        {
          return Convert.ToDateTime(items[0]).ToShortDateString();
        }
        else
        {
          return string.Empty;
        }
      }
    }

    private static List<string> FieldToList(object value)
    {
      if (value != null)
      {

        if (value.GetType() == typeof(string))
        {
          return new List<string>() { (string)value };
        }

        var result = ((JArray)value).ToObject<List<string>>();

        return result;
      }

      return new List<string>();
    }
    private static string GetCDAName(object value)
    {
      try
      {
        var items = FieldToList(value);
        if (items.Count > 0)
        {
          var id = items[0];

          var result = CDATeam.Where(cda => cda.Id == id).FirstOrDefault();
          return (result == null ? "" : result.Name);
        }
        else
        {
          return string.Empty;
        }
      }
      catch (Exception)
      {
        return string.Empty;
      }
    }
    private static async Task GetAirTableTeamDataAsync()
    {
      // Tablename: "Team Roster" -> CDA Name
      // fields: CDA Name, Twitter Handle, GitHub User Name, Photo, Bio

      var fields = new List<string>() { "CDA Name", "Twitter Handle", "GitHub User Name", "Photo", "Bio" };
      var records = await GetAirTableDataAsync("Team Roster", fields);

      CDATeam = (records.Select(r => new CDA()
      {
        Name = (string)r.GetField("CDA Name"),
        Id = r.Id,
        Twitter = (string)r.GetField("Twitter Handle"),
        GitHub = (string)r.GetField("GitHub User Name"),
        Bio = (string)r.GetField("Bio")
      })).ToList();
    }

    private static async Task GetTopicsDataAsync()
    {
      // Tablename: "Topics" -> Topic Areas
      // Fields: Name

      var fields = new List<string>() { "Name" };
      var records = await GetAirTableDataAsync("Topics", fields);

      Topics = (records.Select(r => new Topic()
      {
        Name = (string)r.GetField("Name"),
        Id = r.Id
      })).ToList();
    }

    private static async Task GetTechnologiesDataAsync()
    {
      // Tablename: "Technologies" -> Technologies
      // Fields: Name
      var fields = new List<string>() { "Name" };
      var records = await GetAirTableDataAsync("Technologies", fields);

      Technologies = (records.Select(r => new Technology()
      {
        Name = (string)r.GetField("Name"),
        Id = r.Id
      })).ToList();
    }
    private static string Bash(string cmd)
    {
      var escapedArgs = cmd.Replace("\"", "\\\"");

      var process = new Process()
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "/bin/bash",
          Arguments = $"-c \"{escapedArgs}\"",
          RedirectStandardOutput = true,
          UseShellExecute = false,
          CreateNoWindow = true,
        }
      };
      process.Start();
      string result = process.StandardOutput.ReadToEnd();
      process.WaitForExit();
      return result;
    }
  }
}
