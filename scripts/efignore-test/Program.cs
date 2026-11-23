using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

var mb = new ModelBuilder();
mb.Entity<Card>(e => e.ToTable("Cards"));
mb.Entity<Employee>().ToTable("Employees");
// metadata-API FK (the ComposeCrossModule pattern)
var model = (IMutableModel)mb.Model;
var dep = model.FindEntityType(typeof(Card))!;
var prin = model.FindEntityType(typeof(Employee))!;
var prop = dep.FindProperty(nameof(Card.EmployeeId))!;
var fk = dep.AddForeignKey(prop, prin.FindPrimaryKey()!, prin);
fk.DeleteBehavior = DeleteBehavior.SetNull;
mb.Ignore(typeof(Employee));
var final = mb.Model;
Console.WriteLine("Entities: " + string.Join(",", final.GetEntityTypes().Select(t => t.DisplayName())));
var cardEt = final.FindEntityType(typeof(Card))!;
Console.WriteLine("FKs on Card: " + cardEt.GetForeignKeys().Count());
Console.WriteLine("Navs on Card: " + cardEt.GetNavigations().Count());

public class Employee { public int Id { get; set; } public string Name { get; set; } = ""; }
public class Card { public int Id { get; set; } public string EmployeeId { get; set; } = ""; public Employee? Employee { get; set; } }
