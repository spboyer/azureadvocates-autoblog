using autoblog.Model;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using System.Threading.Tasks;

namespace autoblog.Creator
{
  internal class Post
  {
    public List<ContentItem> Items { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime FromDate { get; set; }

    public string FileName
    {
      get
      {
        return String.Concat(DateTime.Now.ToString("yyyy-MM-dd"), "-CDA-Weekly-WrapUp.md");
      }
    }

    public List<string> Tags { get; set; }

    public Post(List<ContentItem> items)
    {
      Items = items;
      FindFromToDates();
      GetFilteredTags();
    }

    private void GetFilteredTags()
    {
      Tags = new List<string>();
      foreach (var item in Items)
      {
        Tags.AddRange(from tech in item.Technologies
                      where !Tags.Contains(tech.Replace("  ", " ").Replace("(", "").Replace(")", ""))
                      select tech.Replace("  ", " ").Replace("(", "").Replace(")", ""));
      }
    }

    private void FindFromToDates()
    {
      for (int i = 0; i < Items.Count; i++)
      {
        var from = Convert.ToDateTime(Items[i].TargetCompletionDate);
        var to = Convert.ToDateTime(Items[i].TargetCompletionDate);

        if (i == 0)
        {
          FromDate = from;
          ToDate = to;
        }
        else
        {
          if (from <= FromDate)
          {
            FromDate = from;
          }

          if (to >= ToDate)
          {
            ToDate = to;
          }
        }
      }
    }
    public async Task<string> Create()
    {
      var post = new StringBuilder();
      post.AppendLine("---");
      post.AppendLine("layout: post");
      post.AppendLine($"title: CDA Weekly Content Wrapup ({DateTime.Now.ToShortDateString()})");
      post.AppendLine("subtitle: ");
      post.Append("tags: [").AppendJoin(", ", Tags.ToArray()).Append("]");
      post.AppendLine();
      post.AppendLine("---");

      foreach (var contentItem in Items)
      {
        post.AppendLine(await AddFormattedItemAsync(contentItem));
      }

      return post.ToString();
    }

    private async Task<string> AddFormattedItemAsync(ContentItem item)
    {
      var sb = new StringBuilder();
      sb.AppendLine("");

      if (item.Links.Count > 0)
      {
        sb.AppendLine($"## [{item.Title.Trim()}]({item.Links[0]})");
      }
      else
      {
        sb.AppendLine($"# {item.Title.Trim()}");
      }
      sb.AppendLine();

      sb.AppendLine($"**by: {item.CDAName}**");
      sb.AppendLine();

      sb.AppendLine($"{item.Description.Trim()}");
      sb.AppendLine();


      if (item.Technologies.Count > 0)
      {
        sb.Append("Tags: ");
        sb.AppendJoin(", ", item.Technologies.ToArray());
        sb.AppendLine();
        sb.AppendLine();
      }

      if (item.Links.Count > 0 && !string.Equals(item.Links[0], "N/A"))
      {

        if (item.Links[0].ToLower().Contains("youtube.com/watch?v"))
        {
          sb.AppendLine($"<iframe width=\"560\" height=\"315\" src=\"{item.Links[0].Replace("watch?v=","embed/")}\" frameborder=\"0\" allow=\"accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe>");
          sb.AppendLine();
        }
        else
        {
          sb.AppendLine($"[Read More]({item.Links[0]})");
          sb.AppendLine();
        }
      }

      if (item.CTALinks.Count > 0)
      {
        sb.AppendLine("Related Links:");
        foreach (var link in item.CTALinks)
        {
          if (!string.Equals(link.ToUpper(), "N/A"))
          {
            var title = await GetLinkTitleAsync(link.Trim());
            if (!string.IsNullOrEmpty(title))
            {
              sb.AppendLine($"* [{title}]({link})");
            }
            else
            {
              sb.AppendLine($"* [{link}]({link})");
            }
          }
        }
      }

      return sb.ToString();
    }

    private async Task<string> GetLinkTitleAsync(string url)
    {
      try
      {
        var webGet = new HtmlWeb();
        var document = await webGet.LoadFromWebAsync(url);
        var title = document.DocumentNode.SelectSingleNode("html/head/title").InnerText;

        return title.Replace("|", ":");
      }
      catch (Exception)
      {
        return string.Empty;
      }
    }

  }
}