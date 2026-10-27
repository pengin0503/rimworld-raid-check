using RaidCheck.Core;
using static RaidCheck.Tests.Program;
namespace RaidCheck.Tests;
static class BatchTests
{
    internal static void Run()
    {
        Test("batch runs factions sequentially and retains independent results",()=>{
            var created=new List<CoreTests.Source>();
            var batch=new BatchPreviewSession(()=>{var s=new CoreTests.Source();created.Add(s);return s;});
            var c=CoreTests.Choice;
            True(batch.Check(new[]{c,c with {FactionId="tribal"}}));Equal(1,created.Count);
            for(int i=0;i<10;i++)batch.Advance(true);
            Equal(2,created.Count);Equal(10,created[0].Samples);Equal(0,created[1].Samples);
            for(int i=0;i<10;i++)batch.Advance(true);
            Equal(2,batch.Items.Count);Equal("tribal",batch.Items[1].Choice.FactionId);Equal(PreviewState.Completed,batch.State);
        });
        Test("batch skips a failed capture and continues",()=>{
            int n=0;var batch=new BatchPreviewSession(()=>{n++;return new CoreTests.Source{CaptureError=n==1?new PreviewException("bad"):null};});
            var c=CoreTests.Choice;True(batch.Check(new[]{c,c with {FactionId="tribal"}}));
            for(int i=0;i<20;i++)batch.Advance(true);
            Equal(2,batch.Items.Count);Equal(PreviewState.Failed,batch.Items[0].Session.State);Equal(PreviewState.Completed,batch.Items[1].Session.State);
        });
        Test("batch cancellation never starts queued factions",()=>{
            int n=0;var batch=new BatchPreviewSession(()=>{n++;return new CoreTests.Source();});
            var c=CoreTests.Choice;batch.Check(new[]{c,c with {FactionId="tribal"}});batch.Advance(true);batch.Cancel();
            for(int i=0;i<30;i++)batch.Advance(true);Equal(1,n);Equal(PreviewState.Cancelled,batch.State);
        });
        Test("batch rejects empty and duplicate factions before capture",()=>{
            int n=0;var batch=new BatchPreviewSession(()=>{n++;return new CoreTests.Source();});var c=CoreTests.Choice;
            True(!batch.Check(Array.Empty<PreviewChoice>()));True(!batch.Check(new[]{c,c}));Equal(0,n);
        });
        Test("batch with no successful factions reports failure",()=>{
            var batch=new BatchPreviewSession(()=>new CoreTests.Source{CaptureError=new PreviewException("bad")});var c=CoreTests.Choice;
            batch.Check(new[]{c,c with {FactionId="tribal"}});for(int i=0;i<10;i++)batch.Advance(true);Equal(PreviewState.Failed,batch.State);
        });
    }
}
