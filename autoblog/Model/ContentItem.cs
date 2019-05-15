using System;
using System.Collections.Generic;

namespace autoblog.Model
{
  internal class ContentItem
  {
    public ContentItem()
    {
    }

    public string Title { get; set; }
    public string Id { get; set; }
    public string CDAName { get; set; }
    public string TargetCompletionDate { get; set; }
    public List<string> Technologies { get; set; }
    public List<string> ContentType { get; set; }
    public string Description { get; set; }
    public List<string> Links { get; set; }
    public List<string> CTALinks { get; set; }
  }
}