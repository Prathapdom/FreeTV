using System.Net.Http;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.Core;
using Windows.Media.Playback;
namespace FreeTV;
public sealed partial class MainWindow : Window
{
 private static readonly string DataDir=Environment.GetEnvironmentVariable("FREETV_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FreeTV");
 private readonly HttpClient http=new() { Timeout=TimeSpan.FromSeconds(45), MaxResponseContentBufferSize=32*1024*1024 };
 private List<Channel> channels=[]; private Settings state=new(); private Channel? current; private MediaPlayer? player; private ComboBox? themePicker; private int playbackGeneration; private bool ready, favoritesOnly, busy, closed;
 public sealed class Settings { public string Source {get;set;}=""; public string Theme {get;set;}="oled"; public HashSet<string> Favorites {get;set;}=[]; public List<Channel> Channels {get;set;}=[]; public DateTimeOffset? Updated {get;set;} }
 public MainWindow()
 {
  InitializeComponent();
  var area=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
  int width=Math.Min(1420,area.Width-40),height=Math.Min(900,area.Height-40);
  AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X+(area.Width-width)/2,area.Y+(area.Height-height)/2,width,height));
  Video.TransportControls.IsCompact=false;
  var escape=new Microsoft.UI.Xaml.Input.KeyboardAccelerator {Key=Windows.System.VirtualKey.Escape};
  escape.Invoked+=(_,e)=>{if(fullScreen){FullScreenClick(this,new RoutedEventArgs());e.Handled=true;}};
  Root.KeyboardAccelerators.Add(escape);
  Closed+=(_,_)=>{ closed=true; Environment.Exit(0); };
 }
 private void OnLoaded(object sender,RoutedEventArgs e)
 {
  if(ready)return;
  try { if(File.Exists(Path.Combine(DataDir,"settings.json"))) state=JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(DataDir,"settings.json")))??new(); }
  catch(Exception ex){Show("Saved settings could not be read: "+ex.Message,InfoBarSeverity.Warning);}
  state.Favorites ??=[]; channels=state.Channels??[]; ready=true; ApplyTheme(state.Theme);
  if(!string.IsNullOrWhiteSpace(state.Source)) SourceUrl.Text=state.Source;
  UpdateGroups(); UpdateSourceInfo();
  if(channels.Count==0 && !string.IsNullOrWhiteSpace(state.Source))
  {
   SettingsClick(this,new RoutedEventArgs());
   _=LoadSource(state.Source);
  }
  else if(channels.Count==0) SettingsClick(this,new RoutedEventArgs());
 }
 private void Show(string message,InfoBarSeverity severity=InfoBarSeverity.Informational){Notice.Message=message;Notice.Severity=severity;Notice.IsOpen=true;}
 private void EnsureThemePicker()
 {
  if(themePicker!=null)return;
  var host=(StackPanel)((ScrollViewer)SettingsPanel).Content;
  themePicker=new ComboBox { Header="Theme", MinWidth=300, Margin=new Thickness(0,18,0,0) };
  themePicker.Items.Add("OLED Midnight");themePicker.Items.Add("Graphite");themePicker.Items.Add("Arctic Light");
  themePicker.SelectedIndex=state.Theme switch { "graphite"=>1, "arctic"=>2, _=>0 };themePicker.SelectionChanged+=ThemeChanged;host.Children.Insert(6,themePicker);
 }
 private void ThemeChanged(object sender,SelectionChangedEventArgs e)
 {
  if(!ready||themePicker?.SelectedIndex is not (0 or 1 or 2))return;
  state.Theme=themePicker.SelectedIndex==1?"graphite":themePicker.SelectedIndex==2?"arctic":"oled";ApplyTheme(state.Theme);try{Save();}catch{ }
 }
 private void ApplyTheme(string theme)
 {
  var arctic=theme=="arctic";var bg=arctic?"#EDF2F8":theme=="graphite"?"#121417":"#000000";var side=arctic?"#E6EDF5":theme=="graphite"?"#1B1E23":"#050505";var rail=arctic?"#DFE7F1":theme=="graphite"?"#17191D":"#000000";
  Root.RequestedTheme=arctic?ElementTheme.Light:ElementTheme.Dark;Root.Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(bg));((Grid)Root.Children[0]).Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(rail));((Grid)Root.Children[1]).Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(side));((Grid)Root.Children[2]).Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(bg));
 }
 private static Windows.UI.Color ParseColor(string value)=>Windows.UI.Color.FromArgb(255,Convert.ToByte(value[1..3],16),Convert.ToByte(value[3..5],16),Convert.ToByte(value[5..7],16));
 private void Save(){Directory.CreateDirectory(DataDir);var path=Path.Combine(DataDir,"settings.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(state));File.Move(path+".tmp",path,true);}
 private void UpdateSourceInfo()=>SourceInfo.Text=state.Updated is {} date?$"{channels.Count:N0} channels • Last updated {date.ToLocalTime():g}":"No playlist loaded yet.";
 private void UpdateGroups(){Groups.ItemsSource=new[]{"All categories"}.Concat(channels.SelectMany(c=>c.Group.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)).Distinct().OrderBy(g=>g)).ToList();Groups.SelectedIndex=0;Filter();}
 private void Filter(){if(!ready)return;var q=Search.Text.Trim();var group=Groups.SelectedItem as string;var filtered=channels.Where(c=>(!favoritesOnly||state.Favorites.Contains(c.Url))&&(group is null or "All categories"||c.Group.Split(';',StringSplitOptions.TrimEntries).Contains(group))&&(c.Name.Contains(q,StringComparison.OrdinalIgnoreCase)||c.Group.Contains(q,StringComparison.OrdinalIgnoreCase))).ToList();ChannelList.ItemsSource=filtered;Count.Text=$"{filtered.Count:N0} {(favoritesOnly?"favorites":"channels")}";}
 private void FilterChanged(object s,TextChangedEventArgs e)=>Filter(); private void GroupChanged(object s,SelectionChangedEventArgs e)=>Filter();
 private void WatchClick(object s,RoutedEventArgs e){favoritesOnly=false;WatchPanel.Visibility=Visibility.Visible;SettingsPanel.Visibility=Visibility.Collapsed;PageTitle.Text="Watch now";Filter();}
 private void FavoritesClick(object s,RoutedEventArgs e){WatchClick(s,e);favoritesOnly=true;PageTitle.Text="Favorites";Filter();}
 private void SettingsClick(object s,RoutedEventArgs e){EnsureThemePicker();WatchPanel.Visibility=Visibility.Collapsed;SettingsPanel.Visibility=Visibility.Visible;PageTitle.Text="Settings";}
 private async void LoadClick(object s,RoutedEventArgs e)=>await LoadSource(SourceUrl.Text.Trim());
 private async void RefreshClick(object s,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(state.Source)){SettingsClick(s,e);return;}await LoadSource(state.Source);}
 private async Task LoadSource(string url)
 {
  if(busy)return;
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")){Show("Enter a valid HTTP or HTTPS playlist URL.",InfoBarSeverity.Error);return;}
  busy=true;LoadButton.IsEnabled=false;Loading.Visibility=Visibility.Visible;Show("Loading playlist…");
  try
  {
   using var response=await http.GetAsync(uri);response.EnsureSuccessStatusCode();var text=await response.Content.ReadAsStringAsync();
   var parsed=await Task.Run(()=>Playlist.Parse(text,response.RequestMessage?.RequestUri??uri));if(closed)return;
   state.Source=url;state.Channels=parsed;state.Updated=DateTimeOffset.Now;channels=parsed;UpdateGroups();UpdateSourceInfo();
   try{Save();Show($"Loaded {channels.Count:N0} channels. Choose one to watch.",InfoBarSeverity.Success);}catch(Exception ex){Show("Channels loaded, but settings could not be saved: "+ex.Message,InfoBarSeverity.Warning);}
   WatchClick(this,new RoutedEventArgs());
  }
  catch(Exception ex){if(!closed)Show("Could not load playlist. "+(ex is TaskCanceledException?"The request timed out; try again.":ex.Message),InfoBarSeverity.Error);}
  finally{busy=false;if(!closed){LoadButton.IsEnabled=true;Loading.Visibility=Visibility.Collapsed;}}
 }
 private void ChannelSelected(object s,SelectionChangedEventArgs e){if(ChannelList.SelectedItem is Channel channel)Play(channel);}
 private void Play(Channel channel)
 {
  WatchPanel.Visibility=Visibility.Visible;SettingsPanel.Visibility=Visibility.Collapsed;PageTitle.Text="Watch now";current=channel;ChannelTitle.Text=channel.Name;ChannelDetails.Text=channel.Group;FavoriteButton.IsEnabled=true;UpdateFavorite();Welcome.Visibility=Visibility.Collapsed;Notice.IsOpen=false;PlaybackStatus.Text="CONNECTING…";
  var generation=++playbackGeneration;
  try
  {
   if(player is null)
   {
    player=new MediaPlayer { AutoPlay=true };
    player.MediaOpened+=(_,_)=>DispatcherQueue.TryEnqueue(()=>{if(!closed&&generation==playbackGeneration)PlaybackStatus.Text="PLAYING";});
    player.MediaFailed+=(_,args)=>DispatcherQueue.TryEnqueue(()=>{if(!closed&&generation==playbackGeneration){PlaybackStatus.Text="UNAVAILABLE";Show("This channel could not be played. Try Retry or select another channel. "+args.ErrorMessage,InfoBarSeverity.Error);}});
    player.PlaybackSession.PlaybackStateChanged+=(_,_)=>DispatcherQueue.TryEnqueue(()=>{if(!closed&&generation==playbackGeneration&&player.PlaybackSession.PlaybackState!=MediaPlaybackState.None)PlaybackStatus.Text=player.PlaybackSession.PlaybackState.ToString().ToUpperInvariant();});
    Video.SetMediaPlayer(player);
   }
   player.Pause();
   player.Source=MediaSource.CreateFromUri(new Uri(channel.Url));
   player.Play();
  }
  catch(Exception ex){PlaybackStatus.Text="UNAVAILABLE";Show("Could not start playback: "+ex.Message,InfoBarSeverity.Error);}
 }
 private void RetryClick(object s,RoutedEventArgs e){if(current!=null)Play(current);}
 private bool fullScreen;
 private void FullScreenClick(object s,RoutedEventArgs e)
 {
  fullScreen=!fullScreen;
  AppWindow.SetPresenter(fullScreen?Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen:Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
  Root.Children[0].Visibility=Root.Children[1].Visibility=fullScreen?Visibility.Collapsed:Visibility.Visible;
  var pane=(Grid)Root.Children[2];Grid.SetColumn(pane,fullScreen?0:2);Grid.SetColumnSpan(pane,fullScreen?3:1);pane.Padding=fullScreen?new Thickness(0):new Thickness(30,24,30,24);
  pane.Children[0].Visibility=fullScreen?Visibility.Collapsed:Visibility.Visible;
  WatchPanel.Children[1].Visibility=WatchPanel.Children[2].Visibility=fullScreen?Visibility.Collapsed:Visibility.Visible;
  FullScreenButton.Content=fullScreen?"Exit full screen (Esc)":"Full screen";
 }
 private void UpdateFavorite()=>FavoriteButton.Content=current!=null&&state.Favorites.Contains(current.Url)?"♥ Favorited":"♡ Favorite";
 private void FavoriteClick(object s,RoutedEventArgs e){if(current==null)return;if(!state.Favorites.Add(current.Url))state.Favorites.Remove(current.Url);UpdateFavorite();try{Save();}catch(Exception ex){Show("Could not save favorites: "+ex.Message,InfoBarSeverity.Warning);}if(favoritesOnly)Filter();}
}
