namespace FocusLens.Application.BehavioralIntelligence;
public sealed record BehaviorPauseInterval(DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc);
public sealed class BehaviorWindowTimelineCalculator
{
 public const int WindowActiveSeconds=300;
 public IReadOnlyCollection<BehaviorWindowTimelineItem> Calculate(DateTimeOffset start,IReadOnlyCollection<BehaviorPauseInterval> pauses,DateTimeOffset end,bool final)
 {
  if(end<start) throw new ArgumentException("Timeline end precedes session start.");
  var p=pauses.OrderBy(x=>x.StartedAtUtc).ToArray(); DateTimeOffset prev=start;
  foreach(var x in p){if(x.StartedAtUtc<start||x.EndedAtUtc<=x.StartedAtUtc||x.EndedAtUtc>end||x.StartedAtUtc<prev)throw new ArgumentException("Pause intervals are malformed.");prev=x.EndedAtUtc;}
  var result=new List<BehaviorWindowTimelineItem>(); DateTimeOffset cursor=start,windowStart=start;int used=0,index=1;
  foreach(var pause in p.Append(null)) {DateTimeOffset activeEnd=pause?.StartedAtUtc??end; while(cursor<activeEnd){int need=WindowActiveSeconds-used;var available=activeEnd-cursor;if(available.TotalSeconds>=need){var boundary=cursor.AddSeconds(need);result.Add(new(index++,windowStart,boundary,false));windowStart=boundary;used=0;cursor=boundary;}else{used+=(int)available.TotalSeconds;cursor=activeEnd;}} if(pause is not null){cursor=pause.EndedAtUtc;if(used==0)windowStart=cursor;}}
  if(final&&used>0)result.Add(new(index,windowStart,end,true)); return result;
 }
}
public sealed record BehaviorWindowTimelineItem(int WindowIndex,DateTimeOffset WindowStartUtc,DateTimeOffset WindowEndUtc,bool IsFinal);
