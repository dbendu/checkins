"use strict";

// Ни сборки, ни зависимостей. Всё состояние в одном объекте, на каждое изменение
// перерисовывается весь экран: страница маленькая, а следить за точечными
// обновлениями дороже, чем перерисовать.

const $ = (id) => document.getElementById(id);

const state = {
  categories: [],       // [{ id, title }] из /api/categories
  selected: new Set(),  // выбранные id; пусто — искать во всех
  places: [],
  radiusMeters: null,
  searched: false,
  loading: false,
  checkedIn: new Set(), // id мест, где отметились в этом сеансе
  feed: [],             // мои отметки из /api/checkins
  tab: "search",        // "search" | "feed"
};

// ---------- сеть ----------

async function api(method, path, body) {
  const response = await fetch(path, {
    method,
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
    credentials: "same-origin",
  });

  // На 201 тела нет — json() на пустом ответе бросает, поэтому глушим.
  const data = await response.json().catch(() => null);

  if (!response.ok) {
    const error = new Error(
      (data && (data.detail || data.title)) || `Сервер ответил ${response.status}`);
    error.status = response.status;
    throw error;
  }

  return data;
}

function showNote(text) {
  const el = $("note");
  el.textContent = text || "";
  el.hidden = !text;
}

// ---------- геолокация ----------

// Единственное место, где браузер отказывает по-разному, и на каждый отказ
// у человека должен быть свой понятный совет.
function currentPosition() {
  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
      reject(new Error("Браузер не умеет определять положение."));
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => resolve({
        lat: position.coords.latitude,
        lon: position.coords.longitude,
      }),
      (error) => {
        if (error.code === error.PERMISSION_DENIED) {
          reject(new Error(
            "Доступ к геолокации запрещён. Разрешите его в настройках сайта и попробуйте снова."));
        } else if (error.code === error.TIMEOUT) {
          reject(new Error("Не удалось определить положение за 15 секунд. Попробуйте ещё раз."));
        } else {
          reject(new Error("Не удалось определить положение."));
        }
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
  });
}

// ---------- поиск ----------

async function search() {
  state.loading = true;
  showNote("");
  render();

  try {
    const position = await currentPosition();

    // Координаты в строку запроса всегда через точку. toString в JS от локали
    // не зависит, но собираем параметры явно, чтобы это не сломали позже.
    const query = new URLSearchParams();
    query.set("lat", String(position.lat));
    query.set("lon", String(position.lon));
    for (const id of state.selected) query.append("category", id);

    const result = await api("GET", `/api/places/nearby?${query}`);

    state.places = result.places;
    state.radiusMeters = result.radiusMeters;
    state.searched = true;
    state.checkedIn.clear();
  } catch (error) {
    state.places = [];
    state.searched = false;
    showNote(error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : error.message);
  } finally {
    state.loading = false;
    render();
  }
}

// ---------- лента ----------

async function loadFeed() {
  try {
    state.feed = await api("GET", "/api/checkins");
  } catch (error) {
    state.feed = [];
    showNote(`Не удалось загрузить список отметок: ${error.message}`);
  }
}

// ---------- отметка ----------

async function checkIn(place, button) {
  button.disabled = true;
  button.textContent = "Отмечаю…";
  showNote("");

  try {
    // Место уходит целиком: сервер не переспрашивает справочник и верит тому,
    // что человеку показали. categoryId — идентификатор, а не название с экрана.
    await api("POST", "/api/checkins", {
      placeId: place.id,
      name: place.name,
      categoryId: place.categoryId,
      lat: place.lat,
      lon: place.lon,
      address: place.address,
    });

    state.checkedIn.add(place.id);
    await loadFeed();
  } catch (error) {
    showNote(error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : error.message);

    // Кулдаун — это тоже «вы тут уже были»: кнопку возвращать незачем,
    // повторное нажатие даст ту же ошибку.
    if (error.status === 409) state.checkedIn.add(place.id);
  } finally {
    render();
  }
}

// ---------- вкладки ----------

const TABS = ["search", "feed"];

function renderTabs() {
  for (const tab of TABS) {
    const active = state.tab === tab;

    $(`tab-${tab}-btn`).classList.toggle("on", active);
    $(`tab-${tab}-btn`).setAttribute("aria-selected", String(active));
    $(`tab-${tab}`).hidden = !active;
  }
}

// ---------- отрисовка ----------

function renderCategories() {
  const box = $("categories");
  box.textContent = "";

  const chip = (label, on, onClick) => {
    const button = document.createElement("button");
    button.className = `chip${on ? " on" : ""}`;
    button.textContent = label;
    button.setAttribute("aria-pressed", String(on));
    button.addEventListener("click", onClick);
    box.appendChild(button);
  };

  // «Все» — это просто снятый выбор: пустой список сервер понимает как «во всех».
  chip("Все", state.selected.size === 0, () => {
    state.selected.clear();
    render();
  });

  for (const category of state.categories) {
    chip(category.title, state.selected.has(category.id), () => {
      if (state.selected.has(category.id)) state.selected.delete(category.id);
      else state.selected.add(category.id);
      render();
    });
  }
}

function renderPlaces() {
  const box = $("places");
  box.textContent = "";

  // До первого поиска карточку результатов не показываем вовсе.
  $("results").hidden = !state.searched;
  if (!state.searched) return;

  if (state.places.length === 0) {
    const empty = document.createElement("p");
    empty.className = "empty";
    empty.textContent =
      `В радиусе ${state.radiusMeters} м ничего не нашлось.` +
      (state.selected.size ? " Попробуйте снять фильтр категорий." : "");
    box.appendChild(empty);
    return;
  }

  for (const place of state.places) {
    const row = document.createElement("div");
    row.className = "place";

    const text = document.createElement("div");
    text.className = "place-text";

    const name = document.createElement("span");
    name.className = "place-name";
    name.textContent = place.name;

    const meta = document.createElement("span");
    meta.className = "place-meta";
    meta.append(place.category, " · ");

    // В OSM адрес проставлен не у всех точек — говорим об этом прямо,
    // а не показываем пустое место.
    if (place.address) {
      meta.append(place.address);
    } else {
      const unknown = document.createElement("span");
      unknown.className = "unknown";
      unknown.textContent = "Точный адрес неизвестен";
      meta.appendChild(unknown);
    }

    text.append(name, meta);

    const distance = document.createElement("span");
    distance.className = "place-distance";
    distance.textContent = formatDistance(place.distanceMeters);

    const button = document.createElement("button");
    button.className = "primary check-in";

    const done = state.checkedIn.has(place.id);
    button.textContent = done ? "Отмечено" : "Я здесь";
    button.disabled = done;

    if (!done) button.addEventListener("click", () => checkIn(place, button));

    row.append(text, distance, button);
    box.appendChild(row);
  }
}

function renderFeed() {
  const box = $("feed");
  box.textContent = "";

  if (state.feed.length === 0) {
    const empty = document.createElement("p");
    empty.className = "empty";
    empty.textContent = "Пока пусто. Отметьтесь в первом месте — оно появится здесь.";
    box.appendChild(empty);
    return;
  }

  for (const visit of state.feed) {
    const row = document.createElement("div");
    row.className = "visit";

    const text = document.createElement("div");
    text.className = "place-text";

    const name = document.createElement("span");
    name.className = "place-name";
    name.textContent = visit.name;

    const meta = document.createElement("span");
    meta.className = "place-meta";
    meta.textContent = visit.category;

    text.append(name, meta);

    const when = document.createElement("span");
    when.className = "visit-when";
    when.textContent = formatWhen(visit.createdAt);

    row.append(text, when);
    box.appendChild(row);
  }
}

function render() {
  renderTabs();
  renderCategories();
  renderPlaces();
  renderFeed();

  const button = $("search");
  button.disabled = state.loading;
  button.textContent = state.loading ? "Ищу…" : "Найти рядом";

  if (state.tab === "feed") {
    $("hint").textContent = state.feed.length
      ? `${state.feed.length} ${plural(state.feed.length, "отметка", "отметки", "отметок")}.`
      : "Здесь появятся места, где вы отметились.";
  } else {
    $("hint").textContent = state.radiusMeters
      ? `Ищем в радиусе ${state.radiusMeters} м от вас.`
      : "Нажмите «Найти рядом» — понадобится доступ к геолокации.";
  }
}

// 1 отметка, 2 отметки, 5 отметок.
function plural(n, one, few, many) {
  const mod100 = n % 100;
  const mod10 = n % 10;

  if (mod100 >= 11 && mod100 <= 14) return many;
  if (mod10 === 1) return one;
  if (mod10 >= 2 && mod10 <= 4) return few;
  return many;
}

// Сервер отдаёт время в UTC, показываем в местном.
function formatWhen(iso) {
  const date = new Date(iso);
  const today = new Date().toDateString() === date.toDateString();

  const time = date.toLocaleTimeString("ru", { hour: "2-digit", minute: "2-digit" });
  if (today) return `сегодня, ${time}`;

  return `${date.toLocaleDateString("ru", { day: "numeric", month: "short" })}, ${time}`;
}

function formatDistance(meters) {
  return meters < 1000 ? `${meters} м` : `${(meters / 1000).toFixed(1)} км`;
}

// ---------- старт ----------

$("search").addEventListener("click", search);

for (const tab of TABS) {
  $(`tab-${tab}-btn`).addEventListener("click", () => {
    state.tab = tab;
    render();
  });
}

Promise.all([
  api("GET", "/api/categories")
    .then((categories) => { state.categories = categories; })
    .catch((error) => showNote(`Не удалось загрузить категории: ${error.message}`)),
  loadFeed(),
]).finally(render);
