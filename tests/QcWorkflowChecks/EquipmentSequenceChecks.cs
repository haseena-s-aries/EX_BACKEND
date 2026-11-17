using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
internal static class EquipmentSequenceChecks {
 public static async Task Run(){
 var options=new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;await using var db=new SurveyDbContext(options);
 var project=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();
 AreaEquipment Item(Guid p,Guid area,int n)=>new(){ProjectId=p,AreaId=area,SequenceNo=n,TagNumber=Guid.NewGuid().ToString(),Type="Motor",SerialNumber="MANUFACTURER-UNCHANGED"};
 var historical=Item(project,a,6);var duplicate=Item(project,b,1);db.AreaEquipment.AddRange(historical,duplicate);await db.SaveChangesAsync(true);
 var first=Item(project,b,1);var second=Item(project,a,1);db.AreaEquipment.AddRange(first,second);await db.SaveChangesAsync();
 Check(first.SequenceNo==7&&second.SequenceNo==8,"Project-wide bulk numbers continue across areas");
 Check(first.MarkingNo==1&&second.MarkingNo==1,"Area marking numbers start at one independently in each area");
 db.AreaEquipment.Remove(second);await db.SaveChangesAsync();var third=Item(project,b,1);db.AreaEquipment.Add(third);await db.SaveChangesAsync();Check(third.SequenceNo==9,"Deleted numbers are not reused");
 Check(third.MarkingNo==2,"Area marking numbers continue within the same area");
 var other=Item(Guid.NewGuid(),Guid.NewGuid(),99);db.AreaEquipment.Add(other);await db.SaveChangesAsync();Check(other.SequenceNo==1,"A different project starts at one");
 Check(other.MarkingNo==1,"A different area starts its marking numbers at one");
 Check(historical.SequenceNo==6&&duplicate.SequenceNo==1,"Historical numbering is preserved");Check(first.SerialNumber=="MANUFACTURER-UNCHANGED","Manufacturer serial numbers are unchanged");
 var oldProject=Guid.NewGuid();var old=Item(oldProject,Guid.NewGuid(),20);db.AreaEquipment.Add(old);await db.SaveChangesAsync(true);db.AreaEquipment.Remove(old);await db.SaveChangesAsync();var afterDelete=Item(oldProject,Guid.NewGuid(),1);db.AreaEquipment.Add(afterDelete);await db.SaveChangesAsync();Check(afterDelete.SequenceNo==21,"Deleting legacy highest number preserves its high water before first addition");
 Console.WriteLine("All equipment sequence checks passed."); }
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
}
