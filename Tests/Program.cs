using FreeTV;
try
{
var origin=new Uri("https://example.com/channels/index.m3u");
void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
var parsed=Playlist.Parse("\uFEFF#EXTM3U\r\n#EXTINF:-1 group-title=\"News, World\",Channel One\r\nlive.m3u8\r\n#EXTINF:-1,Duplicate\nhttps://example.com/channels/live.m3u8\n#EXTINF:-1,Other\n#EXTGRP:Sports\nhttps://video.example.com/stream\n#EXTINF:-1,Unsafe\nfile:///C:/test",origin);
Check(parsed.Count==2,"duplicates and unsupported schemes excluded");
Check(parsed[0].Name=="Channel One"&&parsed[0].Group=="News, World","quoted metadata commas preserved");
Check(parsed[0].Url=="https://example.com/channels/live.m3u8","relative URLs resolved");
Check(parsed[1].Group=="Sports","EXTGRP supported");
foreach(var invalid in new[]{"<html>Error</html>","#EXTM3U\n#EXT-X-TARGETDURATION:10\npart.ts","#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=100\nvideo.m3u8","#EXTM3U\n"}){try{Playlist.Parse(invalid,origin);throw new Exception("Invalid input accepted");}catch(FormatException){Console.WriteLine("PASS invalid or stream manifest rejected");}}
if(args.Length>0){using var client=new HttpClient();var text=await client.GetStringAsync(args[0]);var actual=Playlist.Parse(text,new Uri(args[0]));Check(actual.Count>100,"live catalogue loads: "+actual.Count+" channels");Console.WriteLine(string.Join("\n",actual.Take(3)));}
}
catch(Exception ex){Console.Error.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1;}
