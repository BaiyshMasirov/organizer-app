"""Read one liquidity worksheet into the existing organizer import format.

Requires openpyxl. No source workbook is modified.
"""
import argparse
import collections
import datetime as dt
import json
from pathlib import Path
from decimal import Decimal

import openpyxl
from openpyxl.utils.datetime import to_excel

parser = argparse.ArgumentParser()
parser.add_argument("workbook", type=Path)
parser.add_argument("output", type=Path)
parser.add_argument("--sheet", default="Август 2026")
args = parser.parse_args()
workbook = openpyxl.load_workbook(args.workbook, data_only=True)
sheet = workbook[args.sheet]
currencies = {"USD", "USDT", "AED", "RUB", "KGS", "EUR", "CNY"}
records, operations, inherited_dates = [], [], []
previous_date = None
def number(value):
    return float(value) if isinstance(value, (float, int)) and not isinstance(value, bool) else 0
def bank(value):
    text = str(value or "").strip()
    return text if text not in {"", "-", "0"} else None
for row_number, cells in enumerate(sheet.iter_rows(values_only=True), 1):
    if not any(value is not None for value in cells):
        continue
    cells = list(cells)
    key = f"liquidity|{sheet.title}|{row_number}"
    name = str(cells[2] or "").strip().lower()
    record_type = "raw"
    if name.startswith(("покупка ", "продажа ")):
        received, sent = number(cells[4]), number(cells[9])
        credit = "кредит" in str(cells[3] or "").lower()
        date = cells[1]
        if isinstance(date, (float, int)):
            date = openpyxl.utils.datetime.from_excel(date, workbook.epoch)
        if isinstance(date, dt.datetime):
            previous_date = date
        elif credit and date is None and previous_date:
            date = previous_date
            inherited_dates.append(row_number)
        else:
            raise ValueError(f"Row {row_number}: missing operation date")
        pair = name.split()[-1].upper().split("/")
        type_code = ("BUY_" if name.startswith("покупка") else "SELL_") + "_".join(pair)
        if len(pair) != 2 or not set(pair) <= currencies:
            raise ValueError(f"Row {row_number}: unknown currency pair")
        if received < 0 or sent < 0 or not (received > 0 or sent > 0):
            raise ValueError(f"Row {row_number}: invalid amounts")
        buy_currency, sell_currency = str(cells[5]).strip().upper(), str(cells[10]).strip().upper()
        if not received and buy_currency not in currencies:
            buy_currency = pair[0] if name.startswith("покупка") else pair[1]
        if not sent and sell_currency not in currencies:
            sell_currency = pair[1] if name.startswith("покупка") else pair[0]
        if buy_currency not in currencies or sell_currency not in currencies:
            raise ValueError(f"Row {row_number}: invalid currencies")
        note = str(cells[13] or "").strip()
        if credit:
            note = (note + "; " if note else "") + "Кредитное движение из Excel; встречная сторона не указана"
        elif not received or not sent:
            note = (note + "; " if note else "") + "Одностороннее движение из Excel; нулевая сторона сохранена"
        if row_number in inherited_dates:
            note += "; дата взята из предыдущей строки"
        record_type = "operation"
        fee_out, fee_in = number(cells[12]), number(cells[7])
        operations.append(dict(sourceKey=key, companyKind=1, typeCode=type_code,
            occurredAt=date.replace(tzinfo=dt.timezone.utc).isoformat(),
            counterparty=str(cells[3] or "").strip() or None,
            sellCurrency=sell_currency, sellAmount=sent, buyCurrency=buy_currency, buyAmount=received,
            feeAmount=fee_out or fee_in, feeCurrency=sell_currency if fee_out else buy_currency,
            baseCurrencyProfit=0, exchangeRate=number(cells[8]) or None,
            sourceAccount=bank(cells[11]) if sent else None,
            destinationAccount=bank(cells[6]) if received else None, note=note or None))
    raw_cells = [to_excel(value, workbook.epoch) if isinstance(value, dt.datetime) else value for value in cells]
    records.append(dict(sourceKey=key, sourceFile=args.workbook.name, sourceSheet=sheet.title,
        sourceRow=row_number, recordType=record_type, dataJson=json.dumps(dict(cells=raw_cells), ensure_ascii=False)))
payload = dict(records=records, operations=operations, expenses=[])
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(dict(sheet=sheet.title, records=len(records), operations=len(operations),
    inheritedDateRows=inherited_dates, types=dict(collections.Counter(x["typeCode"] for x in operations)),
    creditNetUsdt=str(sum(Decimal(str(x["buyAmount"])) - Decimal(str(x["sellAmount"]))
        for x in operations if "кредит" in (x["counterparty"] or "").lower())),
    output=str(args.output)), ensure_ascii=False))
