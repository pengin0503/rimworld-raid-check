using RaidCheck.Core;
using static RaidCheck.Tests.Program;

namespace RaidCheck.Tests;

static class CoreTests
{
    internal static readonly PreviewChoice Choice = new("pirates", "Siege", "EdgeDrop", 10);
    internal static SampleResult Sample(params PawnEntry[] pawns) => new(1000, pawns);
    internal static PawnEntry Pawn(string id = "A", string label = "same", float power = 50) => new(id, label, power);
    internal static void Run()
    {
        Test("idle advances do not capture or sample", () => {
            var f = new Source(); var s = new PreviewSession(f);
            for (int i=0;i<20;i++) s.Advance(true);
            Equal(0, f.Captures); Equal(0, f.Samples); Equal(PreviewState.Idle, s.State);
        });
        Test("Check captures once, advances one sample, and refuses duplicate starts", () => {
            var f=new Source(); var s=new PreviewSession(f);
            True(s.Check(Choice)); Equal(1,f.Captures); Equal(0,f.Samples);
            True(!s.Check(Choice)); s.Advance(true); Equal(1,f.Samples);
            for(int i=0;i<20;i++)s.Advance(true);
            Equal(10,f.Samples); Equal(1,f.Captures); Equal(PreviewState.Completed,s.State);
        });
        Test("cancel and invalid context prevent future samples", () => {
            foreach (bool cancel in new[]{true,false}) {
                var f=new Source(); var s=new PreviewSession(f);s.Check(Choice);s.Advance(true);
                if(cancel)s.Cancel(); else s.Advance(false);
                for(int i=0;i<20;i++)s.Advance(true);
                Equal(1,f.Samples); Equal(PreviewState.Cancelled,s.State);
            }
        });
        Test("partial failures do not count as zero-size raids", () => {
            var f=new Source { Next=i=> i%2==0 ? Sample(Pawn(),Pawn()) : throw new PreviewException("unavailable") };
            var s=new PreviewSession(f);s.Check(Choice);
            for(int i=0;i<10;i++)s.Advance(true);
            Equal(5,s.Results.Count);Equal(5,s.Failures.Count);Equal(2,s.Summary!.Minimum);Equal(2d,s.Summary.Median);
        });
        Test("all failures have no numeric summary", () => {
            var s=new PreviewSession(new Source { Next=_=>throw new InvalidOperationException("broken") });s.Check(Choice);
            for(int i=0;i<10;i++)s.Advance(true);
            Equal(PreviewState.Failed,s.State);True(s.Summary is null);True(s.UnexpectedError is not null);
        });
        Test("capture failure and invalid sample counts never start", () => {
            var f=new Source { CaptureError=new PreviewException("no-map") };var s=new PreviewSession(f);
            True(!s.Check(Choice));Equal(PreviewState.Failed,s.State);Equal(0,f.Samples);
            f=new Source();s=new PreviewSession(f);True(!s.Check(Choice with { SampleCount=1000000 }));Equal(0,f.Captures);
        });
        Test("summary medians and duplicate labels use Def identities", () => {
            var sum=PreviewSummary.Create(new[]{ Sample(Pawn("A"),Pawn("B")),Sample(Pawn("A"),Pawn("A"),Pawn("A"),Pawn("A")) });
            Equal(2,sum.Minimum);Equal(4,sum.Maximum);Equal(3d,sum.Median);Equal(2,sum.Kinds.Count);
            var a=sum.Kinds.Single(x=>x.DefName=="A");var b=sum.Kinds.Single(x=>x.DefName=="B");
            Equal(2.5,a.MeanCount);Equal(2,a.PresentInSamples);Equal(0.5,b.MeanCount);Equal(1,b.PresentInSamples);
            Equal(1d,PreviewSummary.Create(new[]{Sample(Pawn())}).Median);
        });
        Test("bounded materialization rejects malformed power and empty groups", () => {
            Throws<PreviewException>(()=>SampleMaterializer.Read(new[]{Pawn(power:float.NaN)},100));
            Throws<PreviewException>(()=>SampleMaterializer.Read(new[]{Pawn(power:0)},100));
            Throws<PreviewException>(()=>SampleMaterializer.Read(new[]{Pawn()},float.PositiveInfinity));
            Throws<PreviewException>(()=>SampleMaterializer.Read(Array.Empty<PawnEntry>(),100));
            var count=0;
            IEnumerable<PawnEntry> Infinite(){while(true){count++;yield return Pawn();}}
            Throws<PreviewException>(()=>SampleMaterializer.Read(Infinite(),100,3));Equal(4,count);
        });
        Test("materialization snapshots a mutable input", () => {
            var list=new List<PawnEntry>{Pawn()};var sample=SampleMaterializer.Read(list,100);list.Clear();Equal(1,sample.Pawns.Count);
        });
        Test("random isolation restores after success and lazy enumeration exception", () => {
            var rng=new RandomState(); Equal(4,RandomIsolation.Run(rng,42,()=>4));Equal(0,rng.Depth);Equal(42,rng.LastSeed);
            IEnumerable<PawnEntry> Bad(){yield return Pawn();throw new InvalidOperationException("lazy failure");}
            Throws<InvalidOperationException>(()=>RandomIsolation.Run(rng,12,()=>SampleMaterializer.Read(Bad(),100)));
            Equal(0,rng.Depth);Equal(2,rng.Pops);
        });
        Test("restart keeps only the new captured conditions", () => {
            var s=new PreviewSession(new Source());s.Check(Choice);s.Advance(true);s.Cancel();
            True(s.Check(Choice with { StrategyId="ImmediateAttack" }));Equal(0,s.Results.Count);
            Equal("ImmediateAttack",s.Snapshot!.Choice.StrategyId);True(s.Summary is null);
        });
    }
    internal sealed class Source:IPreviewSource
    {
        public int Captures,Samples;
        public Exception? CaptureError;
        public Func<int,SampleResult> Next=_=>CoreTests.Sample(Pawn());
        public PreviewSnapshot Capture(PreviewChoice c){Captures++;if(CaptureError is not null)throw CaptureError;return new(c,"map",500,"Pirates",c.StrategyId,c.ArrivalId,1000,42);}
        public SampleResult Sample(PreviewSnapshot s,int i){Samples++;return Next(i);}
    }
    private sealed class RandomState:IRandomState
    {
        internal int Depth, LastSeed, Pops;
        public void Push(int seed){Depth++;LastSeed=seed;}
        public void Pop(){Depth--;Pops++;}
    }
}
