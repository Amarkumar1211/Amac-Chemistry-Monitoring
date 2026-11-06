const sampleId = 'KHT-27 MIDDLE';
let furnaceNumber = 1;

const fnMatch = sampleId.match(/(?:FN|Furnace|F)[:\-\s]*(\d+)/i);
if (fnMatch) {
    furnaceNumber = parseInt(fnMatch[1], 10);
    console.log('Matched regex: ' + furnaceNumber);
} else if (sampleId.length >= 3) {
    const thirdLetter = sampleId[2].toUpperCase();
    console.log('Third letter: ' + thirdLetter);
    if (thirdLetter >= 'A' && thirdLetter <= 'Z') {
        furnaceNumber = thirdLetter.charCodeAt(0) - 'A'.charCodeAt(0) + 1;
    }
}
console.log('FurnaceNumber: ' + furnaceNumber);
