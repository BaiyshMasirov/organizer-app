import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const [brokerFile, liquidityFile, outputFile] = process.argv.slice(2);
const currencies = new Set(["AED","CNY","EUR","KGS","RUB","USD","USDT"]);
const result = { records: [], operations: [], expenses: [] };
const norm = v => String(v ?? "").trim().toLowerCase().replace(/ё/g,"е").replace(/\s+/g," ");
const num = v => typeof v === "number" && Number.isFinite(v) ? v : null;
const currency = v => { const x=String(v??"").trim().toUpperCase().replace("EURO","EUR"); return currencies.has(x)?x:null; };
const excelDate = v => new Date(Date.UTC(1899,11,30)+Number(v)*86400000).toISOString();
const safeJson = row => JSON.stringify({cells:row.map(v=>v instanceof Date?v.toISOString():v)});
const typeMap = new Map([
  ["покупка usdt/usd","BUY_USDT_USD"],["продажа usdt/usd","SELL_USDT_USD"],["покупка usdt/aed","BUY_USDT_AED"],["продажа usdt/aed","SELL_USDT_AED"],
  ["покупка usdt/rub","BUY_USDT_RUB"],["продажа usdt/rub","SELL_USDT_RUB"],["покупка usd/rub","BUY_USD_RUB"],["продажа usd/rub","SELL_USD_RUB"],
  ["кредит usdt/usdt","CREDIT_USDT"],["кредит usdt","CREDIT_USDT"],["ликвидность usdt/usdt","LIQUIDITY_USDT"],["пополнение ликвидности","LIQUIDITY_USDT"],
  ["конвертация (продажа) usd/aed","SELL_USD_AED"],["продажа usd/aed","SELL_USD_AED"],["конвертация (покупка) usd/aed","BUY_USD_AED"],["покупка usd/aed","BUY_USD_AED"]
]);

async function sheetNames(wb){const x=await wb.inspect({kind:"sheet",include:"id,name",maxChars:30000});return x.ndjson.split(/\r?\n/).filter(Boolean).map(v=>JSON.parse(v).name);}
function findHeader(values, predicate){for(let i=0;i<Math.min(values.length,20);i++)if(values[i].some(predicate))return i;return -1;}
function col(headers, regex, start=0){for(let i=start;i<headers.length;i++)if(regex.test(norm(headers[i])))return i;return -1;}
function noteFrom(row,index){return index>=0&&row[index]!=null?String(row[index]).slice(0,500):null;}

async function readLiquidity(file){
 const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(file));const sourceFile=path.basename(file);
 for(const sheetName of await sheetNames(wb)){const values=wb.worksheets.getItem(sheetName).getUsedRange()?.values??[];const headerIndex=findHeader(values,v=>norm(v)==="операция");const headers=headerIndex>=0?values[headerIndex]:[];
  const opCol=col(headers,/^операция$/),clientCol=col(headers,/^клиент/),dateCol=col(headers,/^дата$/),receiveAmountCol=col(headers,/сумма получ/),receiveCurrencyCol=col(headers,/^валюта$/,receiveAmountCol+1),sourceAccountCol=col(headers,/^bank$/,receiveCurrencyCol+1),sourceFeeCol=col(headers,/комиссия банка/,sourceAccountCol+1),rateCol=col(headers,/^курс$/),sendAmountCol=col(headers,/сумма к отправ/),sendCurrencyCol=col(headers,/^валюта$/,sendAmountCol+1),destinationAccountCol=col(headers,/^bank$/,sendCurrencyCol+1),destinationFeeCol=col(headers,/комиссия банка/,destinationAccountCol+1),noteCol=headers.length-1;
  for(let r=0;r<values.length;r++){const row=values[r];if(!row.some(v=>v!==null&&v!==""))continue;const key=`liquidity|${sheetName}|${r+1}`;let recordType="raw";const opName=opCol>=0?norm(row[opCol]):"";const mappedType=typeMap.get(opName);
   if(mappedType&&num(row[dateCol])&&num(row[receiveAmountCol])>0&&num(row[sendAmountCol])>0&&currency(row[receiveCurrencyCol])&&currency(row[sendCurrencyCol])){recordType="operation";const feeOut=num(row[destinationFeeCol]);const feeIn=num(row[sourceFeeCol]);const fee=feeOut??feeIn??0;const feeCurrency=feeOut!=null?currency(row[sendCurrencyCol]):currency(row[receiveCurrencyCol]);result.operations.push({sourceKey:key,companyKind:1,typeCode:mappedType,occurredAt:excelDate(row[dateCol]),counterparty:String(row[clientCol]??"").trim().slice(0,180)||null,sellCurrency:currency(row[sendCurrencyCol]),sellAmount:num(row[sendAmountCol]),buyCurrency:currency(row[receiveCurrencyCol]),buyAmount:num(row[receiveAmountCol]),feeAmount:fee,feeCurrency:feeCurrency??"USD",baseCurrencyProfit:0,exchangeRate:num(row[rateCol]),sourceAccount:String(row[sourceAccountCol]??"").trim().slice(0,160)||null,destinationAccount:String(row[destinationAccountCol]??"").trim().slice(0,160)||null,note:noteFrom(row,noteCol)});}
   if(sheetName.toLowerCase().includes("расход")&&r>headerIndex&&num(row[1])&&num(row[2])>0&&currency(row[3])){recordType="expense";const c=currency(row[3]);result.expenses.push({sourceKey:key,companyKind:1,occurredAt:excelDate(row[1]),category:String(row[5]??"Прочие расходы").slice(0,120),amount:num(row[2]),currency:c,baseCurrencyAmount:c==="USD"||c==="USDT"?num(row[2]):0,account:String(row[4]??"Без счета").slice(0,150),note:String(row[6]??"").slice(0,300)||null});}
   result.records.push({sourceKey:key,sourceFile,sourceSheet:sheetName,sourceRow:r+1,recordType,dataJson:safeJson(row)});
  }
 }
}

async function readBroker(file){
 const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(file));const sourceFile=path.basename(file);
 for(const sheetName of await sheetNames(wb)){if(norm(sheetName)==="статистика")continue;const values=wb.worksheets.getItem(sheetName).getUsedRange()?.values??[];const headerIndex=findHeader(values,v=>/клиент/.test(norm(v)));if(headerIndex<0)continue;const h=values[headerIndex];const dateCol=col(h,/^дата$/),clientCol=col(h,/клиент/),opCol=col(h,/^операция$/),amountCol=col(h,/сумма поступив/),incomingCurrencyCol=col(h,/^валюта$/,amountCol+1),usdtCol=col(h,/usdt подлежащ|сумма usdt куплен/),profitCol=col(h,/прибыль ориента/),providerCol=col(h,/поставщик/),noteCol=col(h,/примечание/);
  for(let r=0;r<values.length;r++){const row=values[r];if(!row.some(v=>v!==null&&v!==""))continue;const key=`broker|${sheetName}|${r+1}`;let recordType="raw";const d=num(row[dateCol]),client=String(row[clientCol]??"").trim(),incoming=num(row[amountCol]),ccy=currency(row[incomingCurrencyCol]),usdt=num(row[usdtCol]);const op=opCol>=0?norm(row[opCol]):"покупка клиентом";
   if(d&&client&&incoming>0&&usdt>0&&["USD","AED","RUB"].includes(ccy)&&(/покупка клиентом/.test(op)||opCol<0)){recordType="operation";result.operations.push({sourceKey:key,companyKind:0,typeCode:`SELL_USDT_${ccy}`,occurredAt:excelDate(d),counterparty:client,sellCurrency:"USDT",sellAmount:usdt,buyCurrency:ccy,buyAmount:incoming,feeAmount:0,feeCurrency:ccy,baseCurrencyProfit:num(row[profitCol])??0,exchangeRate:incoming/usdt,sourceAccount:String(row[providerCol]??"").trim()||null,destinationAccount:null,note:noteFrom(row,noteCol)});}
   result.records.push({sourceKey:key,sourceFile,sourceSheet:sheetName,sourceRow:r+1,recordType,dataJson:safeJson(row)});
  }
 }
}

await readBroker(brokerFile);await readLiquidity(liquidityFile);
await fs.mkdir(path.dirname(outputFile),{recursive:true});await fs.writeFile(outputFile,JSON.stringify(result));
console.log(JSON.stringify({rawRecords:result.records.length,operations:result.operations.length,expenses:result.expenses.length,byCompany:Object.groupBy(result.operations,x=>x.companyKind===0?"broker":"liquidity")},(k,v)=>Array.isArray(v)&&v.length>20?{count:v.length}:v,2));
