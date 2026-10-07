const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
for (const page of ['Create', 'Edit']) {
  const html = fs.readFileSync(`organaizer/Pages/Operations/${page}.cshtml`, 'utf8');
  assert(!html.includes('type="number"'), 'No wheel-sensitive numeric fields');
  for (const prefilled of [false, true]) {
    const ready = [], elements = new Map();
    class Input {
      constructor(id, value = '') { this.id=id; this.name=id; this._value=value; this.events={}; this.dataset={}; this.classList={toggle(){}}; this.options=[]; this.selectedOptions=[]; this.selectionStart=0; }
      get value(){return this._value;}
      set value(v){this._value=String(v);}
      addEventListener(name,fn){(this.events[name]??=[]).push(fn);}
      fire(name,event={}){for(const fn of this.events[name]??[]) fn(event);}
      setSelectionRange(a){this.selectionStart=a;}
    }
    const get=id=>{if(!elements.has(id))elements.set(id,new Input(id)); return elements.get(id);};
    const fields=['sell-amount-view','buy-amount-view','Input_ExchangeRate','Input_FeeAmount','Input_BaseCurrencyProfit'].map(get);
    fields.forEach(f=>f.name=f.id);
    const sell=get('sell-amount-view'), buy=get('buy-amount-view'), rate=get('Input_ExchangeRate');
    sell.value='1000'; buy.value=prefilled?'2345.6789':'0'; rate.value='2';
    const type=get('Input_TypeCode'); type.value='BUY_USDT_USD'; type.selectedOptions=[{dataset:{sell:'USD',buy:'USDT',oneSided:'false',rateMode:'divide',rateLabel:'USD / USDT'}}];
    get('Input_BuyCurrency').value='USDT'; get('buy-currency-choice').value='USDT';
    get('Input_SellAccountId').selectedOptions=[{}]; get('Input_BuyAccountId').selectedOptions=[{}];
    const form=get('operation-form'); form.dataset={aa:'true',copy:'false'}; form.querySelectorAll=()=>fields;
    const context=vm.createContext({document:{addEventListener:(_,fn)=>ready.push(fn),getElementById:get},HTMLInputElement:Input,URLSearchParams,Number,String,Date,console,fetch:()=>{throw Error('Unexpected initial rate reload');}});
    vm.runInContext(fs.readFileSync('organaizer/wwwroot/js/operation-numbers.js','utf8'),context);
    const script=fs.readFileSync('organaizer/wwwroot/js/operation-exchange.js','utf8');
    vm.runInContext(script,context);
    ready.forEach(fn=>fn());
    assert.equal(buy.value,prefilled?'2345.6789':'0','Initial amount preserved');
    sell.fire('input');
    assert.equal(buy.value,prefilled?'2345.6789':'500.00','Purchase divides money by the money/commodity rate');
    buy._value='14 000 000,12345678'; buy.selectionStart=20; buy.fire('input');
    sell.value='9000'; sell.fire('input'); rate.value='3'; rate.fire('input');
    assert.equal(buy.value,'14000000.12345678','Manual receive amount survives sell and rate edits');
    assert.equal(buy._value,'14 000 000,12345678','Grouped display preserves all decimal digits');
    const data=new Map(); form.fire('formdata',{formData:data});
    assert.equal(data.get(buy.name),'14000000.12345678','Submitted amount has no grouping or comma');
    get('calculate-buy').fire('click');
    assert.equal(buy.value,'3000.00','Explicit purchase recalculation divides by the rate');
    assert.equal(buy._value,'3 000,00','Calculated amounts are grouped');
    type.selectedOptions[0].dataset.rateMode='multiply';
    get('calculate-buy').fire('click');
    assert.equal(buy.value,'27000.00','Sale recalculation multiplies by the rate');
    console.log(`PASS: ${page}, prefilled=${prefilled}: formatting, manual protection, recalculation, serialization`);
  }
}
