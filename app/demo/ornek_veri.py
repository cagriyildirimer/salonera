#!/usr/bin/env python3
"""Salonera demo ortamını dolu dolu örnek veriyle doldurur.

Her şey programın kendi kapısından (HTTP API) geçer, yani iş kuralları aynen uygulanır.
Geçmiş aylardaki kayıt ve ödemeler için demo sunucusunun saati, veri klasöründeki `demo-saat.txt`
dosyasıyla geçici olarak geri alınır; iş bitince dosya silinir ve saat gerçek zamana döner.
Tarihler bugüne göre hesaplanır: demo ne zaman sıfırlanırsa sıfırlansın takvim güncel görünür.

Kullanım: ornek_veri.py <veri klasörü> [sunucu adresi]
Yalnızca Python standart kütüphanesi kullanılır.
"""
import heapq
import json
import random
import sys
import urllib.error
import urllib.request
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

DATA_DIR = Path(sys.argv[1])
BASE = sys.argv[2] if len(sys.argv) > 2 else "http://127.0.0.1:47800"
CLOCK = DATA_DIR / "demo-saat.txt"
PASSWORD = "Salon2026"
TL = 100  # kuruş

rnd = random.Random(2026)
TODAY = (datetime.now(timezone.utc) + timedelta(hours=3)).date()


# ---------------------------------------------------------------- yardımcılar
def set_clock(moment: datetime | None):
    if moment is None:
        CLOCK.unlink(missing_ok=True)
    else:
        CLOCK.write_text(moment.strftime("%Y-%m-%dT%H:%M:%S"))


def call(method, path, body=None, token=None):
    data = None if body is None else json.dumps(body).encode()
    headers = {"content-type": "application/json"}
    if token:
        headers["authorization"] = "Bearer " + token
    req = urllib.request.Request(BASE + path, method=method, data=data, headers=headers)
    try:
        with urllib.request.urlopen(req) as res:
            raw = res.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"{method} {path}: {e.read().decode(errors='replace')}") from None


def at(day: date, hour=None, minute=None):
    return datetime(day.year, day.month, day.day,
                    hour if hour is not None else rnd.randint(10, 18),
                    minute if minute is not None else rnd.choice([0, 5, 10, 15, 20, 30, 40, 45, 50]))


def phone():
    return f"05{rnd.choice(['30', '32', '33', '35', '36', '42', '44', '05', '06', '52', '55'])} " \
           f"{rnd.randint(100, 999)} {rnd.randint(10, 99)} {rnd.randint(10, 99)}"


def round_to(amount, step):
    return max(step, int(round(amount / step)) * step)


# ---------------------------------------------------------------- isimler
BRIDES = ["Elif", "Zeynep", "Merve", "Ayşe", "Büşra", "Selin", "Ece", "Gizem", "Buse", "Melis", "Derya", "Esra",
          "Damla", "İrem", "Sevgi", "Nazlı", "Ceren", "Tuğba", "Hande", "Pınar", "Aslı", "Gamze", "Özge", "Dilara",
          "Kübra", "Yağmur", "Sude", "Ebru", "Beyza", "Nur", "Şeyma", "Cansu", "Hilal", "Tuba", "Burcu", "Ezgi"]
GROOMS = ["Mert", "Can", "Burak", "Emre", "Onur", "Kaan", "Oğuz", "Serkan", "Hakan", "Murat", "Cem", "Barış",
          "Tolga", "Furkan", "Yusuf", "Ömer", "Eren", "Batuhan", "Gökhan", "Kerem", "Uğur", "Volkan", "Sinan",
          "Arda", "Berk", "Halil", "İbrahim", "Mustafa", "Ahmet", "Mehmet", "Selim", "Tarık", "Deniz", "Alper"]
SURNAMES = ["Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Yıldırım", "Öztürk", "Aydın", "Özdemir",
            "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Kara", "Koç", "Kurt", "Özkan", "Şimşek", "Polat",
            "Korkmaz", "Erdoğan", "Güneş", "Aksoy", "Tekin", "Bulut", "Keskin", "Ünal", "Uçar", "Acar", "Turan"]
KIDS = ["Ali", "Efe", "Yiğit", "Emir", "Ömer Asaf", "Mirza", "Aras", "Kuzey", "Alparslan", "Çınar"]

NOTES = [
    "Gelin odası saat 16:00'da hazır olsun.",
    "Gelinin annesi tekerlekli sandalye kullanıyor; giriş rampası açık olmalı.",
    "Masalarda beyaz-gül süsleme istendi.",
    "Damadın ailesi Sivas'tan otobüsle gelecek, otopark ayrılsın.",
    "Fotoğrafçı kendi ekibini getiriyor.",
    "Pasta kesiminde maytap istemiyorlar.",
    "Çocuk masası için 2 masa ayrılacak.",
    "Gelin arabası salon önünde 18:30'da.",
    None, None, None, None,
]
MENU_NOTES = [
    "12 çocuk menüsü (köfte-patates), 3 vejetaryen.",
    "2 kişi glütensiz, 1 kişi laktozsuz.",
    "Gelin masasına ayrıca meyve tabağı.",
    "Ana yemek 21:00'de servis edilsin.",
    "Çorba yerine ara sıcak istenebilir, son hafta netleşecek.",
]
CANCEL_REASONS = [
    "Çift ayrılmaya karar verdi.", "Aile içinde vefat oldu, düğün ertelendi.", "Başka salonla anlaştılar.",
    "Damat yurt dışında işe başladı.", "Bütçe yetmedi.",
]


# ---------------------------------------------------------------- katalog
def setup():
    set_clock(at(TODAY - timedelta(days=330), 9, 0))
    call("POST", "/api/setup", {
        "businessName": "Kristal Düğün Salonu", "phone": "0212 555 00 00",
        "address": "Bağdat Cad. No: 128, Kadıköy / İstanbul",
        "adminName": "Ayşe Yönetici", "adminUsername": "ayse", "adminPassword": PASSWORD,
        "halls": [{"name": "Kristal Salon", "capacity": 500}, {"name": "Bahçe", "capacity": 300},
                  {"name": "Teras", "capacity": 180}],
    })
    admin = call("POST", "/api/login", {"username": "ayse", "password": PASSWORD})["token"]
    settings = call("GET", "/api/settings", token=admin)
    settings.update({"taxOffice": "Kadıköy", "taxNumber": "1234567890"})
    try:
        call("PUT", "/api/settings", settings, token=admin)
    except RuntimeError as e:
        print("  ayarlar güncellenemedi:", e)
    for name, user in [("Mehmet Kaya", "mehmet"), ("Elif Yıldız", "elif")]:
        call("POST", "/api/users", {"name": name, "username": user, "password": PASSWORD, "role": "calisan", "isActive": True}, token=admin)
    tokens = [admin] + [call("POST", "/api/login", {"username": u, "password": PASSWORD})["token"] for u in ("mehmet", "elif")]

    def product(kind, name, price, per_guest=0, min_guests=0, desc=None, group=None):
        return call("POST", f"/api/{kind}", {"name": name, "description": desc, "price": price * TL,
                                              "perGuestPrice": per_guest * TL, "minGuests": min_guests,
                                              "groupName": group, "isActive": True}, token=admin)

    packages = {
        "yemekli": product("packages", "Yemekli Düğün Paketi", 50_000, 900, 200, "Salon, DJ, süsleme, gelin odası, ikram"),
        "gold": product("packages", "Gold Düğün Paketi", 90_000, 1_250, 250, "Yemekli paket + canlı müzik, LED ekran, karşılama kokteyli"),
        "kokteyl": product("packages", "Kokteyl Düğün Paketi", 35_000, 450, 150, "Kokteyl ikramı, pasta, DJ"),
        "nisan": product("packages", "Nişan / Kına Paketi", 20_000, 350, 80, "Salon, süsleme, ikram, müzik"),
        "kucuk": product("packages", "Söz / Sünnet Paketi", 12_000, 250, 50, "Salon, süsleme, çay-kahve ikramı"),
    }

    def dishes(*pairs):
        return [{"course": c, "name": n} for c, n in pairs]

    menus = [
        call("POST", "/api/menus", {"name": "Klasik Menü", "description": "Çorba, ara sıcak, tavuk, tatlı", "perGuestPrice": 0, "isActive": True,
             "items": dishes(("Çorba", "Mercimek çorbası"), ("Ara sıcak", "Paçanga böreği"), ("Ana yemek", "Tavuk sote"),
                             ("Ana yemek", "Şehriyeli pilav"), ("Salata", "Mevsim salata"), ("Tatlı", "Fırın sütlaç"), ("İçecek", "Ayran, meşrubat"))}, token=admin),
        call("POST", "/api/menus", {"name": "Kuzu Tandır Menüsü", "description": "Geleneksel düğün sofrası", "perGuestPrice": 150 * TL, "isActive": True,
             "items": dishes(("Çorba", "Yayla çorbası"), ("Soğuk başlangıç", "Humus, haydari, acılı ezme"), ("Ara sıcak", "Sigara böreği"),
                             ("Ana yemek", "Kuzu tandır"), ("Ana yemek", "İç pilav"), ("Tatlı", "Künefe"), ("Meyve", "Mevsim meyve tabağı"), ("İçecek", "Ayran, meşrubat, çay"))}, token=admin),
        call("POST", "/api/menus", {"name": "Et Menüsü (Premium)", "description": "Dana bonfile ve özel tatlı", "perGuestPrice": 300 * TL, "isActive": True,
             "items": dishes(("Çorba", "Düğün çorbası"), ("Soğuk başlangıç", "Zeytinyağlı yaprak sarma"), ("Ara sıcak", "Mantarlı krep"),
                             ("Ana yemek", "Dana bonfile, patates püresi"), ("Salata", "Roka salatası"), ("Tatlı", "Fıstıklı baklava"), ("İçecek", "Ayran, meşrubat, çay, kahve"))}, token=admin),
        call("POST", "/api/menus", {"name": "Vejetaryen Menü", "description": "Etsiz, zeytinyağlı ağırlıklı", "perGuestPrice": 50 * TL, "isActive": True,
             "items": dishes(("Çorba", "Domates çorbası"), ("Soğuk başlangıç", "Zeytinyağlı enginar"), ("Ana yemek", "Sebzeli güveç"),
                             ("Ana yemek", "Bulgur pilavı"), ("Tatlı", "Kabak tatlısı"), ("İçecek", "Ayran, meşrubat"))}, token=admin),
        call("POST", "/api/menus", {"name": "Kokteyl Menüsü", "description": "Ayakta ikram", "perGuestPrice": 0, "isActive": True,
             "items": dishes(("Kanepe", "Peynirli kanepe"), ("Kanepe", "Füme somonlu kanepe"), ("Ara sıcak", "Mini pizza"),
                             ("Tatlı", "Mini ekler"), ("İçecek", "Limonata, meşrubat"))}, token=admin),
    ]

    extras = {
        "pasta": [product("extras", n, p, group="Pasta") for n, p in
                  [("Muzlu pasta", 3_500), ("Çilekli pasta", 4_000), ("Frambuazlı pasta", 4_500), ("Çikolatalı pasta", 4_200)]],
        "muzik": [product("extras", n, p, group="Müzik") for n, p in
                  [("DJ", 8_000), ("Canlı orkestra", 25_000), ("Fasıl ekibi", 18_000)]],
        "tekli": [
            product("extras", "Fotoğraf ve video", 25_000),
            product("extras", "Kuruyemiş ikramı", 1_000, 20),
            product("extras", "Karşılama kokteyli", 0, 60),
            product("extras", "Havai fişek gösterisi", 12_000),
            product("extras", "Gelin arabası süsleme", 3_000),
            product("extras", "LED ekran ve sanal duvar", 7_500),
        ],
    }
    return tokens, call("GET", "/api/halls", token=admin), packages, menus, extras


# ---------------------------------------------------------------- takvim planı
def build_events(halls):
    """Hangi gün hangi salonda ne var: geçmiş 10 ay ve gelecek 10 ay."""
    events = []
    start, end = TODAY - timedelta(days=300), TODAY + timedelta(days=300)
    day = start
    while day <= end:
        ahead = (day - TODAY).days
        fill = 1.0 if ahead <= 0 else max(0.25, 1 - ahead / 360)  # uzak gelecekte daha az kayıt
        wd = day.weekday()
        for i, hall in enumerate(halls):
            base = {5: [0.85, 0.75, 0.6], 6: [0.65, 0.55, 0.5], 4: [0.3, 0.35, 0.4]}.get(wd, [0.04, 0.05, 0.08])[i]
            if rnd.random() < base * fill:
                events.append((day, i))
        day += timedelta(days=1)
    # Bugün ve bu hafta mutlaka biraz hareket olsun.
    have = {(d, h) for d, h in events}
    for d, h in [(TODAY, 0), (TODAY, 2), (TODAY + timedelta(days=1), 1), (TODAY + timedelta(days=2), 0),
                 (TODAY + timedelta(days=4), 2)]:
        if (d, h) not in have:
            events.append((d, h))
    events.sort()
    return events


def event_shape(hall_index, day):
    if hall_index == 0:
        kind = rnd.choices(["dugun", "nisan", "kokteyl"], [8, 1, 1])[0]
    elif hall_index == 1:
        kind = rnd.choices(["dugun", "nisan", "kina", "kokteyl"], [5, 2, 2, 1])[0]
    else:
        kind = rnd.choices(["nisan", "kina", "soz", "sunnet", "kokteyl", "diger"], [4, 4, 2, 2, 1, 1])[0]
    if day.weekday() == 4 and kind == "dugun":
        kind = "kina"
    cap = [500, 300, 180][hall_index]
    guests = {"dugun": (220, cap), "kokteyl": (120, min(cap, 300)), "nisan": (80, min(cap, 220)),
              "kina": (70, min(cap, 200)), "soz": (40, 110), "sunnet": (80, 180), "diger": (40, 150)}[kind]
    guests = round_to(rnd.randint(guests[0], guests[1]), 10)
    if kind in ("dugun", "kokteyl", "kina"):
        start, end = rnd.choice([("19:00", "23:30"), ("19:30", "00:00"), ("20:00", "00:00"), ("18:30", "23:00")])
    elif kind == "nisan":
        start, end = rnd.choice([("14:00", "18:00"), ("19:00", "23:00"), ("15:00", "19:00")])
    else:
        start, end = rnd.choice([("13:00", "17:00"), ("14:00", "18:00"), ("12:00", "16:00")])
    return kind, guests, start, end


def customer(kind):
    surname = rnd.choice(SURNAMES)
    if kind in ("dugun", "nisan", "kina", "soz", "kokteyl"):
        bride, groom = rnd.choice(BRIDES), rnd.choice(GROOMS)
        full = f"{bride} & {groom} {surname}" if kind == "dugun" and rnd.random() < 0.6 else f"{bride} {surname}"
        return {"fullName": full, "phone": phone(), "phone2": phone() if rnd.random() < 0.4 else None,
                "brideName": bride, "groomName": f"{groom} {rnd.choice(SURNAMES)}", "notes": None}
    if kind == "sunnet":
        return {"fullName": f"{rnd.choice(GROOMS)} {surname}", "phone": phone(), "phone2": None,
                "brideName": None, "groomName": None, "notes": f"Sünnet çocuğu: {rnd.choice(KIDS)}"}
    return {"fullName": f"{rnd.choice(BRIDES + GROOMS)} {surname}", "phone": phone(), "phone2": None,
            "brideName": None, "groomName": None, "notes": "Şirket yıl sonu yemeği" if rnd.random() < 0.5 else None}


def sale_for(kind, packages, menus, extras):
    pkg = {"dugun": rnd.choices(["yemekli", "gold"], [3, 1])[0], "kokteyl": "kokteyl", "nisan": "nisan",
           "kina": "nisan", "soz": "kucuk", "sunnet": "kucuk", "diger": "kucuk"}[kind]
    chosen = []
    if kind in ("dugun", "kokteyl", "nisan") and rnd.random() < 0.75:
        chosen.append(rnd.choice(extras["pasta"]))
    if kind in ("dugun", "kina", "nisan") and rnd.random() < 0.7:
        chosen.append(rnd.choices(extras["muzik"], [5, 2, 2])[0])
    for e in extras["tekli"]:
        if rnd.random() < (0.35 if kind == "dugun" else 0.12):
            chosen.append(e)
    menu = None
    if kind == "kokteyl":
        menu = menus[4]
    elif kind in ("dugun", "nisan", "kina", "sunnet"):
        menu = rnd.choices(menus[:4], [5, 3, 2, 1])[0]
    discount = rnd.choice([0, 0, 0, 0, 5_000, 7_500, 10_000, 15_000]) * TL
    return {"packageId": packages[pkg], "extraIds": chosen, "menuId": menu, "discount": discount}


def method():
    return rnd.choices(["nakit", "havale", "kredi_karti"], [5, 3, 2])[0]


# ---------------------------------------------------------------- doldurma
def main():
    print("Katalog, salonlar ve kullanıcılar…")
    tokens, halls, packages, menus, extras = setup()
    events = build_events(halls)
    print(f"{len(events)} organizasyon planlandı, kayıtlar zamanında giriliyor…")

    # Her iş (kayıt aç, ödeme al, iptal…) gerçekleştiği ana yazılır; sonra zaman sırasıyla yürütülür.
    # Bir iş yürürken yenisini planlayabilir (ör. kayıt açılınca taksit ödemeleri), bu yüzden öncelik kuyruğu.
    actions = []  # (an, sıra, iş)
    seq = 0

    def plan(moment, fn):
        nonlocal seq
        seq += 1
        heapq.heappush(actions, (moment, seq, fn))

    def set_installments(state, token, booked, day, cancel_at):
        """Ödeme planını kapora + taksitler olarak yazar, kaporayı alır, sonraki ödemeleri vadesine planlar."""
        total = call("GET", f"/api/reservations/{state['id']}", token=token)["summary"]["total"]
        dep = min(round_to(total * rnd.uniform(0.15, 0.3), 5_000 * TL), total)
        rest = total - dep
        inst = [{"label": "Kapora", "dueDate": booked.isoformat(), "amount": dep}]
        last_due = max(booked, day - timedelta(days=7))
        if rest > 0 and (last_due - booked).days > 60:
            mid = round_to(rest * 0.5, 1_000 * TL)
            inst.append({"label": "2. taksit", "dueDate": (booked + (last_due - booked) / 2).isoformat(), "amount": mid})
            rest -= mid
        if rest > 0:
            inst.append({"label": "Kalan ödeme", "dueDate": last_due.isoformat(), "amount": rest})
        call("PUT", f"/api/reservations/{state['id']}/plan", {"plan": inst}, token=token)
        call("POST", f"/api/reservations/{state['id']}/payments", {"amount": dep, "method": method(), "note": "Kapora"}, token=token)
        # Sonraki taksitler vadesinde (bazen birkaç gün önce/sonra) ödenir; yakın zamandakilerin bir kısmı gecikir.
        for item in inst[1:]:
            due = date.fromisoformat(item["dueDate"])
            pay_day = max(booked + timedelta(days=1), due + timedelta(days=rnd.choice([-5, -2, 0, 0, 0, 1, 3, 6])))
            if pay_day > TODAY or (cancel_at and pay_day >= cancel_at.date()):
                continue
            if (TODAY - due).days < 20 and rnd.random() < 0.2:
                continue  # gecikmiş taksit
            split = rnd.random() < 0.2 and item["amount"] > 20_000 * TL

            def pay(item=item, state=state, split=split):
                amount = item["amount"]
                if split:
                    part = round_to(amount * 0.5, 1_000 * TL)
                    call("POST", f"/api/reservations/{state['id']}/payments", {"amount": part, "method": method(), "note": None}, token=rnd.choice(tokens))
                    amount -= part
                call("POST", f"/api/reservations/{state['id']}/payments", {"amount": amount, "method": method(), "note": None}, token=rnd.choice(tokens))
            plan(at(pay_day), pay)

    for day, hall_index in events:
        kind, guests, start, end = event_shape(hall_index, day)
        ahead = (day - TODAY).days
        lead = rnd.randint(40, 260) if kind == "dugun" else rnd.randint(15, 120)
        booked = day - timedelta(days=lead)
        if ahead <= 0:
            booked = min(booked, TODAY - timedelta(days=1))
        elif booked > TODAY:
            # Çok ileri tarihli kayıtlar son birkaç haftada açılmış olsun (hepsi bugüne yığılmasın).
            booked = TODAY - timedelta(days=rnd.randint(0, 30))
        booked_at = at(booked)
        if booked_at.date() == day:
            booked_at = at(day, 9, 0)
        state = {"id": None}
        token = rnd.choice(tokens)
        cust = customer(kind)
        sale = sale_for(kind, packages, menus, extras)
        body = {"newCustomer": cust, "hallId": halls[hall_index]["id"], "date": day.isoformat(), "start": start, "end": end,
                "kind": kind, "guestCount": guests, "notes": rnd.choice(NOTES)}

        # Yakın gelecekteki bazı kayıtlar hâlâ ön rezervasyon (bir kısmının süresi dolmuş).
        is_option = 3 <= ahead <= 120 and rnd.random() < 0.14
        via_option = not is_option and (TODAY - booked).days > 8 and (day - booked).days > 30 and rnd.random() < 0.2
        cancel_at = None
        if not is_option and rnd.random() < 0.06:
            c = booked + timedelta(days=rnd.randint(5, max(6, (min(day, TODAY) - booked).days)))
            if c < min(day, TODAY):
                cancel_at = at(c)

        if is_option:
            opened = TODAY - timedelta(days=rnd.randint(0, 9))
            days = rnd.choice([3, 5, 7, 7, 10])

            def create_option(state=state, body=body, token=token, days=days):
                state["id"] = call("POST", "/api/reservations", {**body, "optionDays": days}, token=token)["reservationId"]
            plan(at(opened), create_option)
            continue

        def create_sale(state=state, body=body, token=token, sale=sale, via_option=via_option, booked=booked, day=day, cancel_at=cancel_at):
            if via_option:
                state["id"] = call("POST", "/api/reservations", {**body, "optionDays": 7}, token=token)["reservationId"]
                return
            state["id"] = call("POST", "/api/reservations", {**body, "sale": {**sale, "firstPayment": None}}, token=token)["reservationId"]
            set_installments(state, token, booked, day, cancel_at)

        plan(booked_at, create_sale)
        if via_option:
            confirm_day = booked + timedelta(days=rnd.randint(1, 6))

            def confirm(state=state, token=token, sale=sale, booked=confirm_day, day=day, cancel_at=cancel_at):
                call("POST", f"/api/reservations/{state['id']}/confirm", {**sale, "firstPayment": None}, token=token)
                set_installments(state, token, booked, day, cancel_at)
            plan(at(confirm_day), confirm)

        if cancel_at:
            def cancel(state=state):
                call("POST", f"/api/reservations/{state['id']}/cancel", {"reason": rnd.choice(CANCEL_REASONS)}, token=tokens[0])
                # Bazılarına alınan paranın bir kısmı iade edilir (kapora genelde iade edilmez).
                paid = call("GET", f"/api/reservations/{state['id']}", token=tokens[0])["summary"]["paid"]
                if paid > 0 and rnd.random() < 0.5:
                    amount = round_to(paid * rnd.choice([0.3, 0.5]), 1_000 * TL)
                    call("POST", f"/api/reservations/{state['id']}/refunds",
                         {"amount": min(amount, paid), "method": rnd.choice(["nakit", "havale"]), "reason": "Sözleşme dışı iyi niyet iadesi"}, token=tokens[0])
            plan(cancel_at, cancel)

        # Yaklaşan düğünlerin organizasyon planı ve mutfak notları hazır olsun.
        if 0 <= ahead <= 45 and kind in ("dugun", "nisan", "kina") and not cancel_at:
            def program(state=state, start=start, kind=kind):
                h, m = map(int, start.split(":"))
                t = lambda add: f"{(h + (m + add) // 60) % 24:02d}:{(m + add) % 60:02d}"
                items = [{"time": t(0), "title": "Misafir karşılama", "note": "Karşılama kokteyli girişte"},
                         {"time": t(45), "title": "Gelin ve damadın girişi", "note": None},
                         {"time": t(75), "title": "Yemek servisi", "note": "Önce çorba, 30 dk sonra ana yemek"},
                         {"time": t(150), "title": "Takı töreni" if kind != "kina" else "Kına yakma", "note": None},
                         {"time": t(195), "title": "Pasta kesimi", "note": None}]
                music = [{"label": "Giriş müziği", "song": rnd.choice(["Mabel Matiz – Öyle Kolaysa", "Sezen Aksu – Kaybolan Yıllar", "Can Bonomo – Ruhum Bela"]), "link": None},
                         {"label": "İlk dans", "song": rnd.choice(["Ed Sheeran – Perfect", "Zeki Müren – Gözlerin Doğuyor Gecelerime", "Sertab Erener – Olsun"]), "link": None}]
                try:
                    call("PUT", f"/api/reservations/{state['id']}/program", {"items": items, "music": music, "notes": None}, token=tokens[0])
                    if rnd.random() < 0.6:
                        d = call("GET", f"/api/reservations/{state['id']}", token=tokens[0])
                        if d.get("menuItems"):
                            call("PUT", f"/api/reservations/{state['id']}/menu", {"items": d["menuItems"], "notes": rnd.choice(MENU_NOTES)}, token=tokens[0])
                except RuntimeError as e:
                    print("  plan yazılamadı:", e)
            plan(max(booked_at, at(TODAY, 8, 0)) + timedelta(minutes=10), program)

    errors = 0
    done = 0
    while actions:
        moment, _, fn = heapq.heappop(actions)
        done += 1
        set_clock(moment)
        try:
            fn()
        except (RuntimeError, TypeError, KeyError) as e:
            errors += 1
            if errors <= 15:
                print(f"  atlandı ({moment:%d.%m.%Y}): {e}")
        if done % 250 == 0:
            print(f"  {done} işlem…")
    set_clock(None)

    admin = tokens[0]
    dash = call("GET", "/api/dashboard", token=admin)
    print(f"Tamam: {done} işlem, {errors} atlandı.")
    print(f"Bugün {len(dash['today'])} organizasyon, bu hafta {dash['weekCount']}, "
          f"{len(dash['options'])} ön rezervasyon bekliyor, {len(dash['overdue'])} müşterinin ödemesi gecikmiş.")


if __name__ == "__main__":
    try:
        main()
    finally:
        set_clock(None)
