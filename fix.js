const fs = require('fs');
const file = 'd:/Servidores/FloreriaBautista_Dev/Backend/03_floreria_seeds_pruebas.sql';
let content = fs.readFileSync(file, 'utf8');
content = content.replace(/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/g, (match) => {
    return match.toLowerCase()
        .replace(/a/g, '1')
        .replace(/b/g, '2')
        .replace(/c/g, '3')
        .replace(/d/g, '4')
        .replace(/e/g, '5')
        .replace(/f/g, '6');
});
fs.writeFileSync(file, content);
