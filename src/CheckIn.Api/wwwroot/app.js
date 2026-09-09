"use strict";

// Ни сборки, ни зависимостей. Всё состояние в одном объекте, на каждое изменение
// перерисовывается весь экран: страница маленькая, а следить за точечными
// обновлениями дороже, чем перерисовать.

const $ = (id) => document.getElementById(id);

const state = {
  categories: [],      // [{ id, title }] из /api/categories
  selected: new Set(), // выбранные id; пусто — искать во всех
  places: [],
  radiusMeters: null,
  searched: false,
  loading: false,
};

// ---------- сеть ----------

async function api(path) {
  const response = await fetch(path, { credentials: "same-origin" });
  const data = await response.json().catch(() => null);

  if (!response.ok) {
    throw new Error(
      (data && (data.detail || data.title)) || `Сервер ответил ${response.status}`);
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

    const result = await api(`/api/places/nearby?${query}`);

    state.places = result.places;
    state.radiusMeters = result.radiusMeters;
    state.searched = true;
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

    row.append(text, distance);
    box.appendChild(row);
  }
}

function render() {
  renderCategories();
  renderPlaces();

  const button = $("search");
  button.disabled = state.loading;
  button.textContent = state.loading ? "Ищу…" : "Найти рядом";

  $("hint").textContent = state.radiusMeters
    ? `Ищем в радиусе ${state.radiusMeters} м от вас.`
    : "Нажмите «Найти рядом» — понадобится доступ к геолокации.";
}

function formatDistance(meters) {
  return meters < 1000 ? `${meters} м` : `${(meters / 1000).toFixed(1)} км`;
}

// ---------- старт ----------

$("search").addEventListener("click", search);

api("/api/categories")
  .then((categories) => { state.categories = categories; })
  .catch((error) => showNote(`Не удалось загрузить категории: ${error.message}`))
  .finally(render);
