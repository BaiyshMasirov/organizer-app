document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('operation-form');
  if (!form) return;

  const type = document.getElementById('Input_TypeCode');
  const sell = document.getElementById('Input_SellCurrency');
  const buy = document.getElementById('Input_BuyCurrency');
  const sellView = document.getElementById('sell-currency-view');
  const buyChoice = document.getElementById('buy-currency-choice');
  const sellAmount = document.getElementById('sell-amount-view');
  const buyAmount = document.getElementById('buy-amount-view');
  const sellAccount = document.getElementById('Input_SellAccountId');
  const buyAccount = document.getElementById('Input_BuyAccountId');
  const sellPanel = document.getElementById('sell-panel-column');
  const buyPanel = document.getElementById('buy-panel-column');
  const rate = document.getElementById('Input_ExchangeRate');
  const date = document.getElementById('Input_OccurredAt');
  const rateHint = document.getElementById('rate-hint');
  const calculateButton = document.getElementById('calculate-buy');
  const isAa = form.dataset.aa === 'true';
  const number = value => Number(String(value).replace(/[\s ]/g, '').replace(',', '.')) || 0;
  let initial = true;
  let buyManual = number(buyAmount.value) !== 0;
  let rateRequest = 0;

  buyAmount.addEventListener('input', () => { buyManual = true; });
  calculateButton.addEventListener('click', () => {
    buyManual = false;
    calculateBuy();
  });

  function filter(select, currency) {
    [...select.options].forEach((option, index) => {
      if (!index) return;
      const unavailable = option.dataset.currency !== currency;
      option.hidden = unavailable;
      option.disabled = unavailable;
    });
    if (select.selectedOptions[0]?.disabled) select.value = '';
  }

  function refreshAccounts() {
    document.getElementById('sell-account-currency').textContent = `· ${sell.value}`;
    document.getElementById('buy-account-currency').textContent = `· ${buy.value}`;
    filter(sellAccount, sell.value);
    filter(buyAccount, buy.value);
  }

  async function loadRate(recalculate = true) {
    const selectedType = type.selectedOptions[0];
    if (!isAa || selectedType.dataset.oneSided === 'true') {
      if (selectedType.dataset.oneSided === 'true') rate.value = '1';
      return;
    }

    const request = ++rateRequest;
    const query = new URLSearchParams({ handler: 'Rate', typeCode: type.value, date: date.value });
    try {
      const response = await fetch(`?${query}`);
      const data = await response.json();
      if (request !== rateRequest) return;
      rate.value = data.found ? Number(data.rate).toFixed(8) : '0';
      rateHint.textContent = data.found
        ? `Курс ${data.source} на дату операции · ${data.label}`
        : 'Курс не найден — укажите вручную';
      if (recalculate) calculateBuy();
    } catch {
      if (request === rateRequest) rateHint.textContent = 'Не удалось загрузить курс — укажите вручную';
    }
  }

  function calculateBuy() {
    const selectedType = type.selectedOptions[0];
    const exchangeRate = number(rate.value);
    if (buyManual || !isAa || exchangeRate <= 0 || selectedType.dataset.oneSided === 'true') return;
    const result = selectedType.dataset.rateMode === 'divide'
      ? number(sellAmount.value) / exchangeRate
      : number(sellAmount.value) * exchangeRate;
    buyAmount.value = result.toFixed(2);
  }

  function pair() {
    const selectedType = type.selectedOptions[0];
    const oneSided = selectedType.dataset.oneSided === 'true';
    sellPanel.classList.toggle('d-none', oneSided);
    buyPanel.classList.toggle('col-lg-6', !oneSided);
    buyPanel.classList.toggle('col-12', oneSided);
    if (oneSided) {
      const selectedCurrency = buyChoice.value || buy.value || selectedType.dataset.buy;
      sell.value = selectedCurrency;
      sellAmount.value = '0';
      sellAccount.value = '';
      buyChoice.disabled = false;
      buy.value = selectedCurrency;
      rateHint.textContent = 'Одностороннее поступление';
    } else {
      sell.value = selectedType.dataset.sell;
      buy.value = selectedType.dataset.buy;
      sellView.value = sell.value;
      buyChoice.value = buy.value;
      buyChoice.disabled = true;
      if (number(rate.value) > 0) rateHint.textContent = `Курс · ${selectedType.dataset.rateLabel}`;
    }
    refreshAccounts();
    if (!initial || number(rate.value) <= 0) loadRate(true);
    initial = false;
  }

  buyChoice.value = buy.value;
  buyChoice.addEventListener('change', () => {
    buy.value = buyChoice.value;
    if (type.selectedOptions[0].dataset.oneSided === 'true') sell.value = buy.value;
    refreshAccounts();
  });
  type.addEventListener('change', pair);
  date.addEventListener('change', () => loadRate(true));
  sellAmount.addEventListener('input', calculateBuy);
  rate.addEventListener('input', () => {
    ++rateRequest;
    calculateBuy();
  });
  form.addEventListener('submit', () => { buy.value = buyChoice.value || buy.value; });
  pair();
});
