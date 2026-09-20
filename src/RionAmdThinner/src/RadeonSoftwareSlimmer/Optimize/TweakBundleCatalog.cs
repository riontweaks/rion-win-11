using System.Collections.Generic;
using System.Linq;
namespace RadeonSoftwareSlimmer.Optimize;
public static class TweakBundleCatalog {
 public static IReadOnlyList<string> Sections {get;}=new[]{"Performance & power","Graphics & gaming","MMCSS"};
 public static IReadOnlyList<SystemTweak> Source=>GeneralTweakCatalog.All;
 public static IReadOnlyList<SystemTweak> Unassigned {get;}=new SystemTweak[0];
 public static IReadOnlyList<string> MissingIds {get;}=new string[0];
 public static IReadOnlyList<TweakBundle> All {get;}=Build();
 public static int TweakCount=>Source.Count;
 public static IEnumerable<TweakBundle> InSection(string section)=>All.Where(b=>b.Section==section);
 private static IReadOnlyList<TweakBundle> Build(){
  var result=new List<TweakBundle>();
  result.Add(new("bundle-priority-separation","Win32 Separation Control","Choose foreground scheduling; Restore recovers the saved state.","Performance & power","Performance",Source.Where(t=>t.Id=="priority-separation").ToList()));
  result.Add(new("bundle-game-capture","Game recording & capture","Control background Game DVR and app capture.","Graphics & gaming","Graphics",Source.Where(t=>t.Id.StartsWith("gamedvr-")||t.Id=="appcapture-off").ToList()));
  foreach(var task in MultimediaTweakCatalog.TaskNames){var prefix="mmcss-"+MultimediaTweakCatalog.TaskId(task)+"-";result.Add(new("bundle-"+prefix.TrimEnd('-'),"MMCSS · "+task,"Only threads registered with this task use these scheduling settings.","MMCSS","MMCSS",Source.Where(t=>t.Id.StartsWith(prefix)).ToList()));}
  var responsiveness=Source.Single(t=>t.Id=="system-responsiveness");result.Add(new("bundle-system-responsiveness",responsiveness.Title,responsiveness.Summary,"MMCSS","MMCSS",new[]{responsiveness}));return result;
 }
}