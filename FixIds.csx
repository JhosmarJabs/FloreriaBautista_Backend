using System.IO;
using System.Text.RegularExpressions;

var path = @"d:\Servidores\FloreriaBautista_Dev\Backend\03_floreria_seeds_pruebas.sql";
var content = File.ReadAllText(path);
content = Regex.Replace(content, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", match => {
    var s = match.Value.ToLower();
    s = s.Replace('a', '1')
         .Replace('b', '2')
         .Replace('c', '3')
         .Replace('d', '4')
         .Replace('e', '5')
         .Replace('f', '6');
    return s;
});
File.WriteAllText(path, content);
