using System.Text.RegularExpressions;
namespace FreeTV;
public sealed record Channel(string Name, string Group, string Url)
{
 public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => s[0])).ToUpperInvariant();
}
public static class Playlist
{
 public static List<Channel> Parse(string text, Uri origin)
 {
  if (!text.TrimStart('\uFEFF', ' ', '\r', '\n').StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase)) throw new FormatException("The source is not an M3U playlist.");
  if (text.Contains("#EXT-X-TARGETDURATION") || text.Contains("#EXT-X-STREAM-INF")) throw new FormatException("This is a video stream manifest. Add a channel catalogue such as the IPTV-org index.m3u.");
  var result = new List<Channel>(); var seen = new HashSet<string>(StringComparer.Ordinal);
  string? name = null; string group = "Other";
  foreach (var raw in text.Split('\n'))
  {
   var line = raw.Trim().TrimStart('\uFEFF');
   if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
   {
    bool quoted = false; int comma = -1;
    for (int i=0;i<line.Length;i++) { if(line[i]=='"') quoted=!quoted; if(line[i]==',' && !quoted) { comma=i; break; } }
    name = comma >= 0 ? line[(comma+1)..].Trim() : "Unnamed channel";
    var match=Regex.Match(line, "group-title=\"([^\"]*)\"", RegexOptions.IgnoreCase);
    group=match.Success && match.Groups[1].Value.Length>0 ? match.Groups[1].Value : "Other";
   }
   else if(line.StartsWith("#EXTGRP:")) group=line[8..].Trim();
   else if(line.Length>0 && !line.StartsWith('#'))
   {
    if(Uri.TryCreate(origin,line,out var uri) && uri.Scheme is "http" or "https" && seen.Add(uri.AbsoluteUri)) result.Add(new Channel(string.IsNullOrWhiteSpace(name)?uri.Host:name,group,uri.AbsoluteUri));
    name=null;group="Other";
   }
  }
  if(result.Count==0) throw new FormatException("No HTTP or HTTPS channels were found in this playlist.");
  return result;
 }
}
