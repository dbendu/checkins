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
  placeErrors: new Map(), // id места -> что пошло не так при отметке именно в нём
  feed: [],             // мои отметки из /api/checkins
  visits: [],           // места с отметками всех пользователей, для карты
  tab: "search",        // "search" | "feed" | "map"
  me: null,             // профиль из /api/auth/me, null — не вошли
  mode: "login",        // "login" | "register" — какой режим формы входа
  photoVersion: 0,      // растёт после загрузки: адрес фотографии не меняется
};

// Адрес фотографии постоянный, поэтому после замены браузер показал бы старую —
// версия в строке запроса заставляет его перечитать.
const photoUrl = (userId) => `/api/users/${userId}/photo?v=${state.photoVersion}`;

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

// ---------- вход ----------

function showAuthNote(text) {
  const el = $("auth-note");
  el.textContent = text || "";
  el.hidden = !text;
}

async function loadMe() {
  try {
    state.me = await api("GET", "/api/auth/me");
  } catch (error) {
    if (error.status !== 401) throw error;
    state.me = null;
  }
}

async function submitAuth(event) {
  event.preventDefault();

  const login = $("auth-login").value.trim();
  const password = $("auth-password").value;
  const displayName = $("auth-name").value.trim();

  const register = state.mode === "register";
  const button = $("auth-submit");

  button.disabled = true;
  showAuthNote("");

  try {
    state.me = register
      // Имя не спрашиваем дважды: пустое — значит показываем логин.
      ? await api("POST", "/api/auth/register", { login, password, displayName: displayName || login })
      : await api("POST", "/api/auth/login", { login, password });

    // Пароль не оставляем в поле: форма ещё живёт в DOM под скрытой карточкой.
    $("auth-password").value = "";
    showNote("");

    await loadFeed();
    await loadVisits();
  } catch (error) {
    showAuthNote(error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : error.message);
  } finally {
    button.disabled = false;
    render();
  }
}

function toggleAuthMode() {
  state.mode = state.mode === "login" ? "register" : "login";
  showAuthNote("");
  render();
}

async function logout() {
  try {
    await api("POST", "/api/auth/logout");
  } catch (error) {
    showNote(error.message);
  }

  state.me = null;
  state.feed = [];
  state.visits = [];
  state.places = [];
  state.searched = false;
  state.checkedIn.clear();
  render();
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
    state.placeErrors.clear();
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
  if (!state.me) {
    state.feed = [];
    return;
  }

  try {
    state.feed = await api("GET", "/api/checkins");
  } catch (error) {
    state.feed = [];
    showNote(`Не удалось загрузить список отметок: ${error.message}`);
  }
}

// Карта общая, поэтому и список для неё — общий, а не только свои отметки.
async function loadVisits({ quiet = false } = {}) {
  if (!state.me) {
    state.visits = [];
    return;
  }

  try {
    state.visits = await api("GET", "/api/checkins/map");
  } catch (error) {
    // Фоновое обновление молчит и оставляет на карте прошлые отметки: ругаться
    // раз в пятнадцать секунд из-за моргнувшей сети — хуже, чем показать старое.
    if (quiet) return;

    state.visits = [];
    showNote(`Не удалось загрузить отметки для карты: ${error.message}`);
  }
}

// Пока карта открыта, чужие отметки подтягиваются сами.
const MAP_REFRESH_MS = 15000;

let mapRefresh = null;

function startMapRefresh() {
  if (mapRefresh === null) mapRefresh = setInterval(refreshVisits, MAP_REFRESH_MS);
}

function stopMapRefresh() {
  if (mapRefresh === null) return;

  clearInterval(mapRefresh);
  mapRefresh = null;
}

async function refreshVisits() {
  // Со скрытой вкладки ходить на сервер незачем: увидеть обновление всё равно
  // некому, а вернувшись, человек получит свежие данные следующим тиком.
  if (document.hidden) return;

  await loadVisits({ quiet: true });
  render();
}

// ---------- отметка ----------

async function checkIn(place, button) {
  button.disabled = true;
  button.textContent = "Отмечаю…";

  // Ошибка от прошлой попытки в этом же месте больше не актуальна.
  state.placeErrors.delete(place.id);

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
    await loadVisits();
  } catch (error) {
    // Ошибка относится к конкретной строке списка, а не ко всей странице:
    // показываем её там, где человек нажимал, а не общим баннером наверху.
    state.placeErrors.set(place.id, error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : error.message);

    // Кулдаун — это тоже «вы тут уже были»: кнопку возвращать незачем,
    // повторное нажатие даст ту же ошибку.
    if (error.status === 409) state.checkedIn.add(place.id);
  } finally {
    render();
  }
}

// ---------- фотография профиля ----------

// Ограничение сервера продублировано здесь намеренно: снимок с телефона легко
// весит больше, и объяснить это до отправки честнее, чем после.
const MAX_PHOTO_BYTES = 2 * 1024 * 1024;

async function uploadPhoto(event) {
  const [file] = event.target.files;
  if (!file) return;

  showNote("");

  try {
    if (file.size > MAX_PHOTO_BYTES) {
      throw new Error("Файл больше 2 МБ — уменьшите картинку и попробуйте снова.");
    }

    const body = new FormData();
    body.append("file", file);

    // Мимо api(): там тело уходит как JSON, а здесь нужен multipart,
    // и Content-Type проставит сам браузер вместе с границей частей.
    const response = await fetch("/api/users/me/photo", {
      method: "PUT",
      body,
      credentials: "same-origin",
    });

    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      throw new Error((problem && problem.detail) || `Сервер ответил ${response.status}`);
    }

    state.me.hasPhoto = true;
    state.photoVersion += 1;

    // hasPhoto для меток приходит из данных карты, а не из профиля: без этого
    // на карте осталась бы буква, пока страницу не перезагрузят.
    await loadVisits();

    // Адрес фотографии не изменился — метки сами бы не перерисовались.
    yandex.drawn = "";
  } catch (error) {
    showNote(error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : `Не удалось загрузить фотографию: ${error.message}`);
  } finally {
    // Иначе повторный выбор того же файла не считается изменением.
    event.target.value = "";
    render();
  }
}

// ---------- удаление отметки ----------

async function deleteVisit(visit, button) {
  button.disabled = true;
  button.textContent = "Удаляю…";

  try {
    await api("DELETE", `/api/checkins/${visit.id}`);

    // Место снова свободно: и кнопка в списке поиска, и прошлый отказ по кулдауну
    // относились к отметке, которой больше нет.
    state.checkedIn.delete(visit.placeId);
    state.placeErrors.delete(visit.placeId);

    await loadFeed();
    await loadVisits();
    showNote("");
  } catch (error) {
    showNote(error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : `Не удалось удалить отметку: ${error.message}`);
  } finally {
    render();
  }
}

// ---------- отрисовка ----------

function renderAuth() {
  const inside = Boolean(state.me);

  $("auth-card").hidden = inside;
  $("me-card").hidden = !inside;
  $("tabs").hidden = !inside;

  if (inside) {
    $("me-name").textContent = state.me.displayName;

    // Разметка уже экранирована внутри avatarHtml — тем же кодом, что и метки карты.
    $("me-photo").innerHTML = avatarHtml({
      userId: state.me.id,
      displayName: state.me.displayName,
      hasPhoto: state.me.hasPhoto,
    });

    $("me-photo").title = state.me.hasPhoto
      ? "Загрузить другую фотографию"
      : "Загрузить фотографию";

    return;
  }

  const register = state.mode === "register";

  $("auth-title").textContent = register ? "Регистрация" : "Вход";
  $("auth-submit").textContent = register ? "Зарегистрироваться" : "Войти";
  $("auth-toggle").textContent = register ? "У меня уже есть аккаунт" : "Зарегистрироваться";
  $("auth-name-field").hidden = !register;

  // Подсказка менеджеру паролей: новый пароль он предложит сгенерировать,
  // существующий — подставить.
  $("auth-password").autocomplete = register ? "new-password" : "current-password";
}

// ---------- вкладки ----------

const TABS = ["search", "feed", "map"];

function renderTabs() {
  for (const tab of TABS) {
    const active = state.tab === tab;

    $(`tab-${tab}-btn`).classList.toggle("on", active);
    $(`tab-${tab}-btn`).setAttribute("aria-selected", String(active));
    $(`tab-${tab}`).hidden = !active || !state.me;
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

    const failure = state.placeErrors.get(place.id);
    if (failure) {
      const error = document.createElement("span");
      error.className = "place-error";
      error.setAttribute("role", "status");
      error.textContent = failure;
      text.appendChild(error);
    }

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

    const remove = document.createElement("button");
    remove.className = "linky";
    remove.textContent = "удалить";
    remove.addEventListener("click", () => deleteVisit(visit, remove));

    row.append(text, when, remove);
    box.appendChild(row);
  }
}

// ---------- карта ----------

// Карта живёт в стороне от общего перерисовывания: SDK грузится один раз,
// объект карты переживает render(), меняются только метки.
//
// Версия API — 2.1, а не 3.0: ключ бесплатного тарифа третьей версией
// не принимается (403 «Invalid api key»), второй — принимается.
const yandex = {
  status: "idle",  // idle | loading | ready | failed
  map: null,
  visits: null,    // коллекция меток с отметками — своя, чтобы чистить только её
  drawn: "",       // по какому набору отметок нарисованы текущие метки
  note: "",
};

async function loadMapSdk() {
  const config = await api("GET", "/api/maps/config");

  if (!config.apiKey) {
    throw new Error("Карта не настроена: на сервере не задан ключ Яндекс.Карт.");
  }

  await new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src =
      `https://api-maps.yandex.ru/2.1/?apikey=${encodeURIComponent(config.apiKey)}&lang=ru_RU`;
    script.onload = resolve;
    script.onerror = () => reject(new Error("Не удалось загрузить Яндекс.Карты."));
    document.head.appendChild(script);
  });

  await ymaps.ready();
}

function createMap() {
  yandex.map = new ymaps.Map(
    $("map"),
    { ...startLocation(), controls: ["zoomControl", "geolocationControl"] },
    // Контейнер меняет размер при переключении вкладок — пусть карта следит сама.
    { autoFitToViewport: "always" });

  // Отметки живут в своей коллекции: её мы очищаем при перерисовке,
  // а метка «вы здесь» лежит рядом и переживает это.
  yandex.visits = new ymaps.GeoObjectCollection();
  yandex.map.geoObjects.add(yandex.visits);
}

// Куда смотреть при открытии: на все отметки, а пока их нет — на центр города.
// Своё положение мы не спрашиваем, так что больше центрировать не на что.
// Белград зашит здесь: город у нас пока один, а гадать по языку браузера
// смысла нет.
const CITY_CENTER = [44.8168, 20.4601];

function startLocation() {
  return state.visits.length > 0
    ? { bounds: fitAll(state.visits) }
    : { center: CITY_CENTER, zoom: 13 };
}

// Рамка вокруг всех отметок, углами юго-запад и северо-восток. Запас примерно
// в 200 метров не даёт зуму улететь в максимум, когда отметка одна или все они
// в одной точке.
function fitAll(visits) {
  const pad = 0.002;
  const lats = visits.map((visit) => visit.lat);
  const lons = visits.map((visit) => visit.lon);

  return [
    [Math.min(...lats) - pad, Math.min(...lons) - pad],
    [Math.max(...lats) + pad, Math.max(...lons) + pad],
  ];
}

function drawMarkers() {
  // drawMarkers зовётся на каждый render(), а пересоздавать метки незачем,
  // пока набор отметок не изменился.
  const drawn = state.visits
    .map((visit) => `${visit.placeId}:${visit.visitors.map((v) => v.userId)}`)
    .join("|");

  if (drawn === yandex.drawn) return;

  yandex.visits.removeAll();
  for (const visit of state.visits) yandex.visits.add(placemark(visit));

  yandex.drawn = drawn;
}

// Метка — стопка аватарок тех, кто здесь отметился. Название места и время
// уходят в подсказку: на карте для них нет места.
function placemark(visit) {
  const name = escapeHtml(visit.name);
  const who = visit.visitors
    .map((visitor) => escapeHtml(`${visitor.displayName} — ${formatWhen(visitor.createdAt)}`))
    .join("<br>");

  // Ширина стопки: аватарка 26 пикселей плюс отступы вокруг.
  const width = 8 + visit.visitors.length * 28;

  return new ymaps.Placemark(
    [visit.lat, visit.lon],
    {
      // Подсказка — для мыши, она появляется по наведению. На телефоне наведения
      // не бывает, поэтому то же самое дублируется балуном: он открывается
      // касанием, а на компьютере — щелчком.
      hintContent: `${name}<br>${who}`,
      balloonContent: `<strong>${name}</strong><br>${who}`,
    },
    {
      iconLayout: ymaps.templateLayoutFactory.createClass(
        `<div class="pin">${visit.visitors.map(avatarHtml).join("")}</div>`),
      // Метка нарисована над точкой, поэтому и область попадания — над ней.
      // По высоте берём с запасом: пальцем целятся хуже, чем курсором.
      iconShape: { type: "Rectangle", coordinates: [[-width / 2, -44], [width / 2, 4]] },
    });
}

// Фотография есть не у всех: вместо неё — первая буква имени на своём цвете.
function avatarHtml(visitor) {
  if (visitor.hasPhoto) {
    return `<img class="ava" src="${photoUrl(visitor.userId)}"`
      + ` alt="${escapeHtml(visitor.displayName)}">`;
  }

  // Цвет от id, а не от имени: у тёзок кружки будут разными.
  const color = `hsl(${(visitor.userId * 137) % 360}deg 35% 45%)`;
  const letter = escapeHtml([...visitor.displayName][0]?.toUpperCase() ?? "?");

  return `<span class="ava ava-empty" style="background:${color}">${letter}</span>`;
}

// В 2.1 макет метки и подсказка задаются строкой HTML, а имена приходят из базы.
// Доллар экранируем вместе с разметкой: в шаблонах Яндекса $[...] — подстановка.
const ESCAPED = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;", $: "&#36;" };

function escapeHtml(text) {
  return String(text).replace(/[&<>"'$]/g, (character) => ESCAPED[character]);
}

function renderMap() {
  if (!state.me || state.tab !== "map") {
    stopMapRefresh();
    return;
  }

  startMapRefresh();

  // Контейнер прячем только вместе с картой: размеры она меряет при создании,
  // а у скрытого блока они нулевые.
  $("map").hidden = yandex.status === "failed";

  // Отметок может ещё не быть — карту всё равно показываем, просто пустую,
  // а строка над ней объясняет, почему на ней ничего нет.
  $("map-empty").hidden = state.visits.length > 0 || yandex.status !== "ready";

  $("map-note").textContent = yandex.note;
  $("map-note").hidden = !yandex.note;

  if (yandex.status === "ready") drawMarkers();
  else if (yandex.status === "idle") startMap();
}

async function startMap() {
  yandex.status = "loading";

  try {
    await loadMapSdk();

    createMap();
    drawMarkers();

    yandex.status = "ready";
  } catch (error) {
    // Повторных попыток нет намеренно: render() зовётся часто, и авто-повтор
    // превратился бы в бесконечный цикл запросов.
    yandex.status = "failed";
    yandex.note = error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : error.message;
  }

  render();
}

function render() {
  renderAuth();
  renderTabs();
  renderCategories();
  renderPlaces();
  renderFeed();
  renderMap();

  const button = $("search");
  button.disabled = state.loading;
  button.textContent = state.loading ? "Ищу…" : "Найти рядом";

  if (!state.me) {
    $("hint").textContent = "Войдите или зарегистрируйтесь, чтобы отмечаться в местах.";
    return;
  }

  if (state.tab === "map") {
    $("hint").textContent = yandex.status === "loading"
      ? "Загружаю карту…"
      : "Отметки всех пользователей — ваши и чужие.";
  } else if (state.tab === "feed") {
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
$("auth-form").addEventListener("submit", submitAuth);
$("auth-toggle").addEventListener("click", toggleAuthMode);
$("logout").addEventListener("click", logout);
$("me-photo").addEventListener("click", () => $("me-photo-file").click());
$("me-photo-file").addEventListener("change", uploadPhoto);

for (const tab of TABS) {
  $(`tab-${tab}-btn`).addEventListener("click", () => {
    state.tab = tab;
    render();
  });
}

(async () => {
  try {
    state.categories = await api("GET", "/api/categories");

    await loadMe();
    await loadFeed();
    await loadVisits();
  } catch (error) {
    showNote(error.message === "Failed to fetch"
      ? "Нет связи с сервером."
      : error.message);
  } finally {
    render();
  }
})();
