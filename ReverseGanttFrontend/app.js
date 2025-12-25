function $(id){ return document.getElementById(id); }

function toast(msg){
  const t = $("toast");
  if(!t) return;
  t.textContent = msg;
  t.classList.remove("hidden");
  clearTimeout(window.__toastTimer);
  window.__toastTimer = setTimeout(()=>t.classList.add("hidden"), 2600);
}

function escapeHtml(s){
  return String(s)
    .replaceAll("&","&amp;")
    .replaceAll("<","&lt;")
    .replaceAll(">","&gt;")
    .replaceAll('"',"&quot;")
    .replaceAll("'","&#039;");
}

function getBackend(){
  const el = $("backendUrl");
  return localStorage.getItem("BACKEND_URL") || (el ? el.value.trim() : "") || "http://localhost:5051";
}

function setBackend(url){
  localStorage.setItem("BACKEND_URL", url);
  const el = $("backendUrl");
  if(el) el.value = url;
  toast("Backend сохранён: " + url);
}

function getAuth(){
  try{
    return JSON.parse(localStorage.getItem("AUTH") || "null");
  }catch{
    return null;
  }
}

function setAuth(auth){
  if(!auth){
    localStorage.removeItem("AUTH");
  }else{
    localStorage.setItem("AUTH", JSON.stringify(auth));
  }
  renderAuthState();
  applyRoleGates();
}

function authHeader(){
  const a = getAuth();
  if(!a?.token) return {};
  return { "Authorization": "Bearer " + a.token };
}

async function apiGet(path){
  const url = `${getBackend()}${path}`;
  const res = await fetch(url, { headers: { ...authHeader() } });
  if(!res.ok) throw new Error(`GET ${path} -> ${res.status}\n${await res.text()}`);
  const ct = res.headers.get("content-type") || "";
  if(ct.includes("application/json")) return await res.json();
  return null;
}

async function apiPost(path, body){
  const url = `${getBackend()}${path}`;
  const headers = { ...authHeader() };
  if(body) headers["Content-Type"] = "application/json";
  const res = await fetch(url, {
    method:"POST",
    headers,
    body: body ? JSON.stringify(body) : null
  });
  if(!res.ok) throw new Error(`POST ${path} -> ${res.status}\n${await res.text()}`);
  const ct = res.headers.get("content-type") || "";
  if(ct.includes("application/json")) return await res.json();
  return null;
}

async function apiPut(path, body){
  const url = `${getBackend()}${path}`;
  const res = await fetch(url, {
    method:"PUT",
    headers: { "Content-Type":"application/json", ...authHeader() },
    body: JSON.stringify(body)
  });
  if(!res.ok) throw new Error(`PUT ${path} -> ${res.status}\n${await res.text()}`);
  const ct = res.headers.get("content-type") || "";
  if(ct.includes("application/json")) return await res.json();
  return null;
}

async function apiPatch(path, body){
  const url = `${getBackend()}${path}`;
  const res = await fetch(url, {
    method:"PATCH",
    headers: { "Content-Type":"application/json", ...authHeader() },
    body: JSON.stringify(body)
  });
  if(!res.ok) throw new Error(`PATCH ${path} -> ${res.status}\n${await res.text()}`);
  const ct = res.headers.get("content-type") || "";
  if(ct.includes("application/json")) return await res.json();
  return null;
}

async function apiDelete(path){
  const url = `${getBackend()}${path}`;
  const res = await fetch(url, { method:"DELETE", headers: { ...authHeader() } });
  if(!(res.status === 204 || res.ok)) throw new Error(`DELETE ${path} -> ${res.status}\n${await res.text()}`);
}

function roleName(x){
  return ["Участник","Преподаватель","Капитан"][x] ?? String(x);
}

function roleFromString(s){
  const v = String(s || "").toLowerCase();
  if(v === "teacher") return 1;
  if(v === "captain") return 2;
  if(v === "participant") return 0;
  return 0;
}

function statusName(x){
  return x === 0 ? "Сделана" : "В процессе";
}

function depTypeName(x){
  const n = Number(x);
  if(n === 0) return "SS";
  if(n === 1) return "SF";
  if(n === 2) return "FS";
  if(n === 3) return "FF";
  return String(x);
}

const state = {
  step: localStorage.getItem("ACTIVE_STEP") || "auth",
  activeTeamId: Number(localStorage.getItem("ACTIVE_TEAM_ID") || "0") || null,
  activeProjectId: Number(localStorage.getItem("ACTIVE_PROJECT_ID") || "0") || null,
  teamsCache: [],
  projectsCache: [],
  participantsCache: [],
  projectsById: new Map(),
  participantsById: new Map(),
  tasksCache: [],
  depsCache: [],
  depDnD: { predId: null, succId: null }
};

function setActiveTeam(id){
  state.activeTeamId = id || null;
  localStorage.setItem("ACTIVE_TEAM_ID", String(state.activeTeamId || ""));
  const lbl = $("activeTeamLabel");
  if(lbl) lbl.textContent = state.activeTeamId ? `#${state.activeTeamId}` : "—";
}

function setActiveProject(id){
  state.activeProjectId = id || null;
  localStorage.setItem("ACTIVE_PROJECT_ID", String(state.activeProjectId || ""));
  const lbl = $("activeProjectLabel");
  if(lbl) lbl.textContent = state.activeProjectId ? `#${state.activeProjectId}` : "—";
}

function setStep(step){
  state.step = step;
  localStorage.setItem("ACTIVE_STEP", step);

  ["auth","team","project","people","tasks","timeline"].forEach(s=>{
    const el = $(`step-${s}`);
    if(el) el.classList.toggle("hidden", s !== step);
  });

  document.querySelectorAll(".step").forEach(btn=>{
    const s = btn.dataset.step;
    btn.classList.toggle("active", s === step);

    const a = getAuth();
    const mustAuth = ["team","project","people","tasks","timeline"].includes(s);
    const needsTeam = ["project","people","tasks","timeline"].includes(s);
    const needsProject = ["tasks","timeline"].includes(s);

    let disabled = false;
    if(mustAuth && !a?.token) disabled = true;
    if(needsTeam && !state.activeTeamId) disabled = true;
    if(needsProject && !state.activeProjectId) disabled = true;

    btn.disabled = disabled;
  });
}

function renderAuthState(){
  const el = $("authState");
  if(!el) return;
  const a = getAuth();
  if(!a?.token){
    el.textContent = "—";
    return;
  }
  const r = roleName(roleFromString(a.role));
  el.textContent = `${a.name} • ${a.email} • ${r} • Team #${a.teamId}`;
}

function isCaptain(){
  const a = getAuth();
  return a?.token && roleFromString(a.role) === 2;
}
function isTeacher(){
  const a = getAuth();
  return a?.token && roleFromString(a.role) === 1;
}
function isParticipant(){
  const a = getAuth();
  return a?.token && roleFromString(a.role) === 0;
}

function applyRoleGates(){
  const captain = isCaptain();
  const teacher = isTeacher();
  const participant = isParticipant();

  const teamCreateBtn = $("teamCreateBtn");
  const teamCreateHint = $("teamCreateHint");
  if(teamCreateBtn) teamCreateBtn.disabled = !captain;
  if(teamCreateHint) teamCreateHint.textContent = captain ? "" : "Создание команды доступно капитану";

  const projectCreateBtn = $("projectCreateBtn");
  const projectCreateHint = $("projectCreateHint");
  if(projectCreateBtn) projectCreateBtn.disabled = !captain;
  if(projectCreateHint) projectCreateHint.textContent = captain ? "" : "Создание проекта доступно капитану";

  const participantCreateBtn = $("participantCreateBtn");
  const participantCreateHint = $("participantCreateHint");
  if(participantCreateBtn) participantCreateBtn.disabled = !captain;
  if(participantCreateHint) participantCreateHint.textContent = captain ? "" : "Добавление участников доступно капитану";

  const taskCreateBtn = $("taskCreateBtn");
  const taskCreateHint = $("taskCreateHint");
  if(taskCreateBtn) taskCreateBtn.disabled = !captain;
  if(taskCreateHint) taskCreateHint.textContent = captain ? "" : "Создание задач доступно капитану";

  const depCreateBtn = $("depCreateBtn");
  const depHint = $("depHint");
  if(depCreateBtn) depCreateBtn.disabled = !captain;
  if(depHint) depHint.textContent = captain ? "" : "Зависимости может создавать только капитан";

  const logoutBtn = $("logoutBtn");
  if(logoutBtn) logoutBtn.disabled = !(getAuth()?.token);

  const toTeam = $("toTeamFromAuth");
  if(toTeam) toTeam.disabled = !(getAuth()?.token);

  if(teacher){
    if(taskCreateHint) taskCreateHint.textContent = "Преподаватель может оценивать и комментировать результат задачи";
  }
  if(participant){
    if(taskCreateHint) taskCreateHint.textContent = "Участник может сдавать результат задачи";
  }
}

function localInputToIsoWithOffset(dtLocal){
  if(!dtLocal) return null;
  const d = new Date(dtLocal);
  if(isNaN(d.getTime())) throw new Error("Некорректная дата/время: " + dtLocal);
  const pad = (n)=> String(Math.abs(Math.trunc(n))).padStart(2,"0");
  const y = d.getFullYear();
  const m = pad(d.getMonth()+1);
  const day = pad(d.getDate());
  const h = pad(d.getHours());
  const min = pad(d.getMinutes());
  const s = "00";
  const tz = -d.getTimezoneOffset();
  const sign = tz >= 0 ? "+" : "-";
  const tzh = pad(Math.floor(Math.abs(tz)/60));
  const tzm = pad(Math.abs(tz)%60);
  return `${y}-${m}-${day}T${h}:${min}:${s}${sign}${tzh}:${tzm}`;
}

function isoToLocalInput(iso){
  if(!iso) return "";
  const d = new Date(iso);
  if(isNaN(d.getTime())) return "";
  const pad = (n)=> String(n).padStart(2,"0");
  const y = d.getFullYear();
  const m = pad(d.getMonth()+1);
  const day = pad(d.getDate());
  const h = pad(d.getHours());
  const min = pad(d.getMinutes());
  return `${y}-${m}-${day}T${h}:${min}`;
}

async function loadTeams(){
  const root = $("teamsList");
  if(root) root.textContent = "Загрузка...";
  const items = await apiGet("/api/teams");
  state.teamsCache = items || [];
  if(root){
    root.innerHTML = "";
    state.teamsCache.forEach(t=>{
      const div = document.createElement("div");
      div.className = "row";
      div.innerHTML = `
        <div>
          <b>#${t.id}</b> ${escapeHtml(t.name)}
          ${state.activeTeamId === t.id ? `<span class="pill">Активная</span>` : ""}
        </div>
        <div class="row-actions">
          <button class="btn btn-small">Выбрать</button>
          <button class="btn btn-danger btn-small">Удалить</button>
        </div>
      `;
      const [pickBtn, delBtn] = div.querySelectorAll("button");

      pickBtn.onclick = async ()=>{
        setActiveTeam(t.id);
        setActiveProject(null);
        await refreshForStep("project");
        toast("Активная команда: #" + t.id);
      };

      delBtn.onclick = async ()=>{
        if(!isCaptain()){ toast("Удаление команды доступно капитану"); return; }
        await apiDelete(`/api/teams/${t.id}`);
        if(state.activeTeamId === t.id){
          setActiveTeam(null);
          setActiveProject(null);
        }
        await loadTeams();
        await loadProjects();
      };

      root.appendChild(div);
    });
  }

  const lbl = $("activeTeamLabel");
  if(lbl) lbl.textContent = state.activeTeamId ? `#${state.activeTeamId}` : "—";

  await fillTeamSelects();
}

async function fillTeamSelects(){
  const projectSel = $("projectTeamSelect");
  const pTeamSel = $("pTeamSelect");

  if(projectSel){
    projectSel.innerHTML = "";
    state.teamsCache.forEach(t=>{
      const opt = document.createElement("option");
      opt.value = String(t.id);
      opt.textContent = `#${t.id} ${t.name}`;
      projectSel.appendChild(opt);
    });
    if(state.activeTeamId) projectSel.value = String(state.activeTeamId);
  }

  if(pTeamSel){
    pTeamSel.innerHTML = "";
    state.teamsCache.forEach(t=>{
      const opt = document.createElement("option");
      opt.value = String(t.id);
      opt.textContent = `#${t.id} ${t.name}`;
      pTeamSel.appendChild(opt);
    });
    if(state.activeTeamId) pTeamSel.value = String(state.activeTeamId);
  }
}

async function loadProjects(){
  const root = $("projectsList");
  if(root) root.textContent = "Загрузка...";

  const items = await apiGet("/api/projects");
  state.projectsCache = items || [];
  state.projectsById.clear();
  state.projectsCache.forEach(p => state.projectsById.set(p.id, p));

  const filtered = state.activeTeamId ? state.projectsCache.filter(p => p.teamId === state.activeTeamId) : state.projectsCache;

  if(root){
    root.innerHTML = "";
    filtered.forEach(p=>{
      const dl = p.deadline ? new Date(p.deadline).toLocaleString() : "—";
      const div = document.createElement("div");
      div.className = "row";
      div.innerHTML = `
        <div>
          <b>#${p.id}</b> ${escapeHtml(p.name)} <span class="muted">(${escapeHtml(p.subject)})</span>
          ${state.activeProjectId === p.id ? `<span class="pill">Активный</span>` : ""}
          <div class="muted small">Дедлайн: ${escapeHtml(dl)}</div>
        </div>
        <div class="row-actions">
          <button class="btn btn-small">Выбрать</button>
          <button class="btn btn-danger btn-small">Удалить</button>
        </div>
      `;
      const [pickBtn, delBtn] = div.querySelectorAll("button");

      pickBtn.onclick = async ()=>{
        setActiveProject(p.id);
        await refreshForStep("people");
        toast("Активный проект: #" + p.id);
      };

      delBtn.onclick = async ()=>{
        if(!isCaptain()){ toast("Удаление проекта доступно капитану"); return; }
        await apiDelete(`/api/projects/${p.id}`);
        if(state.activeProjectId === p.id) setActiveProject(null);
        await loadProjects();
      };

      root.appendChild(div);
    });
  }

  const lbl = $("activeProjectLabel");
  if(lbl) lbl.textContent = state.activeProjectId ? `#${state.activeProjectId}` : "—";

  await fillProjectSelects();
}

async function fillProjectSelects(){
  const tProjectSelect = $("tProjectSelect");
  const timelineProjectSelect = $("timelineProjectSelect");

  const filtered = state.activeTeamId ? state.projectsCache.filter(p => p.teamId === state.activeTeamId) : state.projectsCache;

  if(tProjectSelect){
    tProjectSelect.innerHTML = "";
    filtered.forEach(p=>{
      const opt = document.createElement("option");
      opt.value = String(p.id);
      opt.textContent = `#${p.id} ${p.name}`;
      tProjectSelect.appendChild(opt);
    });
    if(state.activeProjectId) tProjectSelect.value = String(state.activeProjectId);
  }

  if(timelineProjectSelect){
    timelineProjectSelect.innerHTML = "";
    filtered.forEach(p=>{
      const opt = document.createElement("option");
      opt.value = String(p.id);
      opt.textContent = `#${p.id} ${p.name}`;
      timelineProjectSelect.appendChild(opt);
    });
    if(state.activeProjectId) timelineProjectSelect.value = String(state.activeProjectId);
  }

  if(state.activeProjectId){
    const p = state.projectsById.get(state.activeProjectId);
    if(p?.deadline){
      const dl = $("tDeadline");
      if(dl && !dl.value) dl.value = isoToLocalInput(p.deadline);
    }
  }
}

async function loadParticipants(){
  const root = $("participantsList");
  if(root) root.textContent = "Загрузка...";

  if(!state.activeTeamId){
    if(root) root.textContent = "Сначала выбери команду.";
    return;
  }

  const items = await apiGet(`/api/participants?teamId=${encodeURIComponent(state.activeTeamId)}`);
  state.participantsCache = items || [];
  state.participantsById.clear();
  state.participantsCache.forEach(p => state.participantsById.set(p.id, p));

  if(root){
    root.innerHTML = "";
    state.participantsCache.forEach(p=>{
      const div = document.createElement("div");
      div.className="row";
      div.innerHTML = `
        <div>
          <b>#${p.id}</b> ${escapeHtml(p.name)}
          <span class="pill">${escapeHtml(roleName(p.role))}</span>
          <div class="muted small">${p.email ?? ""}</div>
        </div>
        <div class="row-actions">
          <button class="btn btn-danger btn-small">Удалить</button>
        </div>
      `;

      const delBtn = div.querySelector("button");
      delBtn.onclick = async ()=>{
        if(!isCaptain()){ toast("Удаление участников доступно капитану"); return; }
        await apiDelete(`/api/participants/${p.id}`);
        await loadParticipants();
        await renderExecutorsChecklist();
      };

      root.appendChild(div);
    });
  }
}

function getSelectedExecutorIds(){
  const box = $("executorsBox");
  if(!box) return [];
  return Array.from(box.querySelectorAll("input[type=checkbox]:checked")).map(x=>Number(x.value));
}

async function renderExecutorsChecklist(){
  const box = $("executorsBox");
  if(!box) return;

  box.innerHTML = "";
  if(!state.activeTeamId){
    box.innerHTML = `<div class="muted small">Выбери команду</div>`;
    return;
  }

  const people = state.participantsCache.length ? state.participantsCache : await apiGet(`/api/participants?teamId=${encodeURIComponent(state.activeTeamId)}`);
  if(!people || people.length === 0){
    box.innerHTML = `<div class="muted small">Добавь участников команды</div>`;
    return;
  }

  people.forEach(p=>{
    const row = document.createElement("div");
    row.className = "checkitem";
    row.innerHTML = `
      <input type="checkbox" value="${p.id}" id="ex_${p.id}">
      <label for="ex_${p.id}">${escapeHtml(p.name)} <span class="muted">(${escapeHtml(roleName(p.role))})</span></label>
    `;
    box.appendChild(row);
  });
}

async function loadTasks(){
  const root = $("tasksList");
  if(root) root.textContent = "Загрузка...";
  if(!state.activeProjectId){
    if(root) root.textContent = "Сначала выбери проект.";
    return;
  }

  const items = await apiGet(`/api/tasks?projectId=${encodeURIComponent(state.activeProjectId)}`);
  state.tasksCache = items || [];

  if(root){
    root.innerHTML = "";
    const a = getAuth();
    const currentParticipantId = a?.participantId;

    const sorted = state.tasksCache.slice().sort((a,b)=> new Date(a.deadline) - new Date(b.deadline));
    for(const t of sorted){
      const execText = taskExecutorsText(t.executorIds);
      const modeText = (t.startDate && t.endDate) ? "Даты заданы" : `Без дат, длительность: ${t.durationDays ?? "?"} дн.`;
      const canSubmit = isParticipant() || isCaptain();
      const canEvaluate = isTeacher();

      const div = document.createElement("div");
      div.className="row";
      div.innerHTML = `
        <div style="flex:1">
          <div>
            <b>#${t.id}</b> ${escapeHtml(t.title)}
            <span class="pill">${escapeHtml(statusName(t.status))}</span>
          </div>
          <div class="muted small" style="margin-top:6px">
            Исполнители: ${escapeHtml(execText)} |
            ${escapeHtml(modeText)} |
            Deadline: ${new Date(t.deadline).toLocaleString()}
          </div>
          <div class="muted small" style="margin-top:8px" id="taskExtra_${t.id}">Загрузка результата...</div>
        </div>
        <div class="row-actions">
          <button class="btn btn-small" data-action="toggle">${t.status === 0 ? "Вернуть" : "Сделать Done"}</button>
          <button class="btn btn-danger btn-small" data-action="delete">Удалить</button>
        </div>
      `;

      const [toggleBtn, delBtn] = div.querySelectorAll("button");

      toggleBtn.onclick = async ()=>{
        if(!isCaptain()){ toast("Менять статус может капитан"); return; }
        const nextStatus = t.status === 0 ? 1 : 0;
        await apiPatch(`/api/tasks/${t.id}/status`, { status: nextStatus });
        await loadTasks();
        await buildDepsUI();
        toast("Статус обновлён");
      };

      delBtn.onclick = async ()=>{
        if(!isCaptain()){ toast("Удалять задачи может капитан"); return; }
        await apiDelete(`/api/tasks/${t.id}`);
        await loadTasks();
        await buildDepsUI();
      };

      root.appendChild(div);

      const extra = $(`taskExtra_${t.id}`);
      if(extra){
        const parts = [];

        let taskResult = null;
        try{
          taskResult = await apiGet(`/api/task-results?taskItemId=${encodeURIComponent(t.id)}`);
        }catch{
          taskResult = null;
        }

        if(taskResult){
          parts.push(`<div><b>Результат:</b> ${escapeHtml(taskResult.resultText ?? "—")}</div>`);
          parts.push(`<div><b>Ссылка:</b> ${taskResult.resultUrl ? `<a href="${escapeHtml(taskResult.resultUrl)}" target="_blank">${escapeHtml(taskResult.resultUrl)}</a>` : "—"}</div>`);
          parts.push(`<div class="muted small">Сдано: ${new Date(taskResult.submittedAt).toLocaleString()}</div>`);
        }else{
          parts.push(`<div class="muted small"><b>Результат:</b> пока не сдан</div>`);
        }

        if(canSubmit){
          const own = (currentParticipantId != null);
          parts.push(`
            <div style="margin-top:10px; padding-top:10px; border-top:1px dashed #eef2f7">
              <div class="muted small"><b>Сдать/обновить результат</b></div>
              <div style="display:grid; grid-template-columns:1fr 1fr; gap:10px; margin-top:6px">
                <input id="resText_${t.id}" placeholder="Описание результата" value="${escapeHtml(taskResult?.resultText ?? "")}"/>
                <input id="resUrl_${t.id}" placeholder="Ссылка (GitHub/Deploy/etc)" value="${escapeHtml(taskResult?.resultUrl ?? "")}"/>
              </div>
              <button class="btn btn-small" type="button" id="resBtn_${t.id}" ${own ? "" : "disabled"}>Отправить</button>
            </div>
          `);
        }

        if(canEvaluate){
          let evalObj = null;
          try{
            evalObj = await apiGet(`/api/evaluations?taskItemId=${encodeURIComponent(t.id)}`);
          }catch{
            evalObj = null;
          }

          parts.push(`
            <div style="margin-top:10px; padding-top:10px; border-top:1px dashed #eef2f7">
              <div class="muted small"><b>Оценка преподавателя</b></div>
              <div style="display:grid; grid-template-columns:160px 1fr; gap:10px; margin-top:6px">
                <input id="evalScore_${t.id}" type="number" min="0" max="100" placeholder="0..100" value="${evalObj?.score ?? ""}"/>
                <input id="evalFb_${t.id}" placeholder="Комментарий к результату" value="${escapeHtml(evalObj?.feedback ?? "")}"/>
              </div>
              <button class="btn btn-small" type="button" id="evalBtn_${t.id}">Сохранить</button>
            </div>
          `);
        }

        extra.innerHTML = parts.join("");

        if(canSubmit){
          const btn = $(`resBtn_${t.id}`);
          if(btn){
            btn.onclick = async ()=>{
              try{
                const text = ($(`resText_${t.id}`)?.value ?? "").trim() || null;
                const url = ($(`resUrl_${t.id}`)?.value ?? "").trim() || null;
                await apiPost("/api/task-results", { taskItemId: t.id, resultText: text, resultUrl: url });
                toast("Результат отправлен");
                await loadTasks();
              }catch(e){
                toast(String(e));
              }
            };
          }
        }

        if(canEvaluate){
          const btn = $(`evalBtn_${t.id}`);
          if(btn){
            btn.onclick = async ()=>{
              try{
                const s = ($(`evalScore_${t.id}`)?.value ?? "").trim();
                const score = s === "" ? null : Number(s);
                const fb = ($(`evalFb_${t.id}`)?.value ?? "").trim() || null;
                await apiPost("/api/evaluations", { taskItemId: t.id, score, feedback: fb });
                toast("Оценка сохранена");
                await loadTasks();
              }catch(e){
                toast(String(e));
              }
            };
          }
        }
      }
    }
  }
}

function taskExecutorsText(executorIds){
  if(!executorIds || executorIds.length === 0) return "Вся команда";
  const names = executorIds.map(id => state.participantsById.get(id)?.name || `#${id}`);
  return names.join(", ");
}

async function loadDependencies(){
  const root = $("depsList");
  if(root) root.textContent = "Загрузка...";
  if(!state.activeProjectId){
    if(root) root.textContent = "Сначала выбери проект.";
    return;
  }

  const deps = await apiGet(`/api/dependencies?projectId=${encodeURIComponent(state.activeProjectId)}`);
  state.depsCache = deps || [];

  if(root){
    root.innerHTML = "";
    if(state.depsCache.length === 0){
      root.innerHTML = `<div class="muted small">Зависимостей пока нет</div>`;
      return;
    }

    for(const d of state.depsCache){
      const pred = state.tasksCache.find(x=>x.id===d.predecessorId);
      const succ = state.tasksCache.find(x=>x.id===d.successorId);
      const predName = pred ? pred.title : `#${d.predecessorId}`;
      const succName = succ ? succ.title : `#${d.successorId}`;

      const div = document.createElement("div");
      div.className = "row";
      div.innerHTML = `
        <div style="flex:1">
          <div><b>${escapeHtml(predName)}</b> → <b>${escapeHtml(succName)}</b></div>
          <div class="muted small">Тип: ${escapeHtml(depTypeName(d.type))} | Offset: ${escapeHtml(String(d.timeOffsetMinutes || 0))} мин</div>
        </div>
        <div class="row-actions">
          <button class="btn btn-danger btn-small">Удалить</button>
        </div>
      `;
      const delBtn = div.querySelector("button");
      delBtn.onclick = async ()=>{
        if(!isCaptain()){ toast("Удалять зависимости может капитан"); return; }
        await apiDelete(`/api/dependencies/${d.id}`);
        await loadDependencies();
        await buildTimeline();
      };
      root.appendChild(div);
    }
  }
}

function resetDepPair(){
  state.depDnD.predId = null;
  state.depDnD.succId = null;
  const pair = $("depPair");
  if(pair) pair.textContent = "—";
}

function setDepPair(predId, succId){
  state.depDnD.predId = predId;
  state.depDnD.succId = succId;

  const pred = state.tasksCache.find(x=>x.id===predId);
  const succ = state.tasksCache.find(x=>x.id===succId);

  const pair = $("depPair");
  if(pair){
    pair.textContent = `${pred ? pred.title : "#" + predId}  →  ${succ ? succ.title : "#" + succId}`;
  }
}

async function buildDepsUI(){
  const dragList = $("depDragList");
  const dropList = $("depDropList");
  if(!dragList || !dropList) return;

  dragList.innerHTML = "";
  dropList.innerHTML = "";

  if(!state.activeProjectId){
    dragList.innerHTML = `<div class="muted small">Сначала выбери проект</div>`;
    dropList.innerHTML = `<div class="muted small">Сначала выбери проект</div>`;
    return;
  }

  const tasks = state.tasksCache.slice().sort((a,b)=> new Date(a.deadline) - new Date(b.deadline));
  if(tasks.length === 0){
    dragList.innerHTML = `<div class="muted small">Нет задач</div>`;
    dropList.innerHTML = `<div class="muted small">Нет задач</div>`;
    return;
  }

  for(const t of tasks){
    const item = document.createElement("div");
    item.className = "dep-item";
    item.draggable = true;
    item.dataset.taskId = String(t.id);
    item.textContent = `#${t.id} ${t.title}`;
    item.addEventListener("dragstart", (e)=>{
      e.dataTransfer.setData("text/plain", String(t.id));
      e.dataTransfer.effectAllowed = "move";
    });
    dragList.appendChild(item);
  }

  for(const t of tasks){
    const item = document.createElement("div");
    item.className = "dep-item dep-drop";
    item.dataset.taskId = String(t.id);
    item.textContent = `#${t.id} ${t.title}`;
    item.addEventListener("dragover", (e)=>{
      e.preventDefault();
      e.dataTransfer.dropEffect = "move";
    });
    item.addEventListener("drop", (e)=>{
      e.preventDefault();
      const predId = Number(e.dataTransfer.getData("text/plain"));
      const succId = Number(t.id);
      if(!predId || !succId) return;
      if(predId === succId){ toast("Нельзя сделать зависимость на саму себя"); return; }
      setDepPair(predId, succId);
    });
    dropList.appendChild(item);
  }

  resetDepPair();
}

async function createDependencyFromPair(){
  if(!isCaptain()){ toast("Зависимости может создавать только капитан"); return; }
  const predId = state.depDnD.predId;
  const succId = state.depDnD.succId;
  if(!predId || !succId){ toast("Сначала перетащи задачу на задачу"); return; }

  const type = Number($("depType")?.value ?? "2");
  const offset = Number(($("depOffset")?.value ?? "0").trim() || "0");

  await apiPost("/api/dependencies", {
    predecessorId: predId,
    successorId: succId,
    type,
    timeOffsetMinutes: offset
  });

  toast("Зависимость создана");
  resetDepPair();
  await loadDependencies();
  await buildTimeline();
}

async function refreshForStep(step){
  try{
    if(step === "auth"){
      renderAuthState();
      applyRoleGates();
    }

    if(step === "team"){
      await loadTeams();
    }

    if(step === "project"){
      await loadTeams();
      await loadProjects();
    }

    if(step === "people"){
      await loadTeams();
      await loadProjects();
      await loadParticipants();
      await renderExecutorsChecklist();
    }

    if(step === "tasks"){
      await loadTeams();
      await loadProjects();
      await loadParticipants();
      await renderExecutorsChecklist();
      await loadTasks();
      await buildDepsUI();
      await loadDependencies();
    }

    if(step === "timeline"){
      await loadTeams();
      await loadProjects();
      await loadParticipants();
      await loadTasks();
      await buildTimeline();
    }
  }catch(err){
    toast(String(err));
  }
}

function floorDay(ms){
  const d = new Date(ms);
  d.setHours(0,0,0,0);
  return d.getTime();
}
function ceilDay(ms){
  const d = new Date(ms);
  d.setHours(23,59,59,999);
  return d.getTime();
}
function dayDiff(aMs,bMs){
  return Math.round((floorDay(bMs) - floorDay(aMs)) / (24*60*60*1000));
}
function weekdayRu(ms){
  return ["Вс","Пн","Вт","Ср","Чт","Пт","Сб"][new Date(ms).getDay()];
}
function daysText(ms){
  const days = Math.max(1, Math.round(ms/(24*60*60*1000)));
  return `${days} дн.`;
}
function taskDurationMsFromDto(t){
  if(t.startDate && t.endDate){
    const s = new Date(t.startDate).getTime();
    const e = new Date(t.endDate).getTime();
    const d = e - s;
    if(Number.isFinite(d) && d > 0) return d;
  }
  const days = t.durationDays && t.durationDays > 0 ? t.durationDays : 1;
  return days * 24*60*60*1000;
}

const depCacheTimeline = new Map();
async function getDepsForSuccessor(taskId){
  if(depCacheTimeline.has(taskId)) return depCacheTimeline.get(taskId);
  const deps = await apiGet(`/api/dependencies/task/${encodeURIComponent(taskId)}`);
  depCacheTimeline.set(taskId, deps);
  return deps;
}

async function computeReverseSchedule(tasksById, targetFinishMs, targetTaskId){
  const reachable = new Set([targetTaskId]);
  const edges = [];
  const stack = [targetTaskId];

  while(stack.length){
    const succId = stack.pop();
    const deps = await getDepsForSuccessor(succId);
    for(const d of deps){
      const predId = d.predecessorId;
      edges.push({
        predId,
        succId,
        type: d.type,
        offsetMs: (d.timeOffsetMinutes || 0) * 60000
      });
      if(!reachable.has(predId)){
        reachable.add(predId);
        stack.push(predId);
      }
    }
  }

  const latestFinish = new Map();
  const latestStart  = new Map();
  const dur = new Map();

  for(const id of reachable){
    const t = tasksById.get(id);
    const d = taskDurationMsFromDto(t);
    dur.set(id, d);
    latestFinish.set(id, Number.POSITIVE_INFINITY);
    latestStart.set(id, Number.POSITIVE_INFINITY);
  }

  const targetDur = dur.get(targetTaskId);
  latestFinish.set(targetTaskId, targetFinishMs);
  latestStart.set(targetTaskId, targetFinishMs - targetDur);

  const MAX_ITERS = 80;
  for(let iter=0; iter<MAX_ITERS; iter++){
    let changed = false;

    for(const e of edges){
      const predId = e.predId;
      const succId = e.succId;

      const succLF = latestFinish.get(succId);
      const succLS = latestStart.get(succId);
      if(!Number.isFinite(succLF) && !Number.isFinite(succLS)) continue;

      const predDur = dur.get(predId);

      let newPredLF = latestFinish.get(predId);
      let newPredLS = latestStart.get(predId);

      if(e.type === 2){
        if(Number.isFinite(succLS)) newPredLF = Math.min(newPredLF, succLS - e.offsetMs);
      } else if(e.type === 0){
        if(Number.isFinite(succLS)) newPredLS = Math.min(newPredLS, succLS - e.offsetMs);
      } else if(e.type === 3){
        if(Number.isFinite(succLF)) newPredLF = Math.min(newPredLF, succLF - e.offsetMs);
      } else if(e.type === 1){
        if(Number.isFinite(succLF)) newPredLS = Math.min(newPredLS, succLF - e.offsetMs);
      }

      if(Number.isFinite(newPredLF)) newPredLS = Math.min(newPredLS, newPredLF - predDur);
      if(Number.isFinite(newPredLS)) newPredLF = Math.min(newPredLF, newPredLS + predDur);

      if(newPredLF !== latestFinish.get(predId) || newPredLS !== latestStart.get(predId)){
        latestFinish.set(predId, newPredLF);
        latestStart.set(predId, newPredLS);
        changed = true;
      }
    }

    if(!changed) break;
  }

  const schedule = [];
  for(const id of reachable){
    const t = tasksById.get(id);
    const ls = latestStart.get(id);
    const lf = latestFinish.get(id);
    if(!Number.isFinite(ls) || !Number.isFinite(lf)) continue;
    schedule.push({
      id,
      title: t.title,
      status: t.status,
      deadline: new Date(t.deadline).getTime(),
      start: ls,
      finish: lf,
      executorIds: t.executorIds || [],
      recommended: true
    });
  }

  schedule.sort((a,b)=>a.finish - b.finish);
  return schedule;
}

async function buildTimeline(){
  const leftBody = $("ganttLeftBody");
  const rightHead = $("ganttRightHead");
  const rightBody = $("ganttRightBody");
  const meta = $("timelineMeta");
  const title = $("ganttTitle");
  const subtitle = $("ganttSubtitle");
  const modeEl = $("timelineMode");
  const projectSel = $("timelineProjectSelect");

  if(!leftBody || !rightHead || !rightBody) return;

  const projectId = Number(projectSel?.value || state.activeProjectId);
  if(!projectId){
    leftBody.innerHTML = "<div class='muted' style='padding:12px'>Выбери проект.</div>";
    rightHead.innerHTML = "";
    rightBody.innerHTML = "";
    if(meta) meta.textContent = "";
    return;
  }

  depCacheTimeline.clear();

  const tasks = await apiGet(`/api/tasks?projectId=${encodeURIComponent(projectId)}`);
  if(!tasks || tasks.length === 0){
    leftBody.innerHTML = "<div class='muted' style='padding:12px'>Нет задач.</div>";
    rightHead.innerHTML = "";
    rightBody.innerHTML = "";
    if(meta) meta.textContent = "";
    return;
  }

  const project = state.projectsById.get(projectId);
  const projectDeadlineMs = project?.deadline ? new Date(project.deadline).getTime() : null;
  const markerDeadlineMs = projectDeadlineMs || Math.max(...tasks.map(t=>new Date(t.deadline).getTime()));

  const tasksById = new Map(tasks.map(t=>[t.id, t]));
  const mode = modeEl?.value || "actual";

  let rows = [];
  if(mode === "actual"){
    rows = tasks.map(t=>{
      const deadlineMs = new Date(t.deadline).getTime();
      if(t.startDate && t.endDate){
        return {
          id: t.id,
          title: t.title,
          status: t.status,
          start: new Date(t.startDate).getTime(),
          finish: new Date(t.endDate).getTime(),
          deadline: deadlineMs,
          executorIds: t.executorIds || [],
          recommended: false
        };
      }else{
        const durMs = taskDurationMsFromDto(t);
        return {
          id: t.id,
          title: t.title,
          status: t.status,
          start: deadlineMs - durMs,
          finish: deadlineMs,
          deadline: deadlineMs,
          executorIds: t.executorIds || [],
          recommended: false
        };
      }
    }).sort((a,b)=>a.deadline - b.deadline);
    if(meta) meta.textContent = `Режим: Факт | Задач: ${rows.length}`;
  }else{
    const targetTask = tasks.slice().sort((a,b)=>new Date(b.deadline)-new Date(a.deadline))[0];
    rows = await computeReverseSchedule(tasksById, markerDeadlineMs, targetTask.id);
    if(meta) meta.textContent = `Режим: Reverse | В цепочке: ${rows.length}`;
  }

  const minStart = Math.min(...rows.map(r=>r.start));
  const maxEnd = Math.max(...rows.map(r=>Math.max(r.finish, r.deadline, markerDeadlineMs)));

  const rangeStart = floorDay(minStart);
  const rangeEnd = ceilDay(maxEnd);
  const totalDays = Math.max(1, dayDiff(rangeStart, rangeEnd) + 1);
  const timelineWidth = totalDays * 44;

  if(title) title.textContent = project ? `Проект: ${project.name}` : `Проект #${projectId}`;
  if(subtitle) subtitle.textContent = `Диапазон: ${totalDays} дн. (с ${new Date(rangeStart).toLocaleDateString()} по ${new Date(rangeEnd).toLocaleDateString()})`;

  rightHead.innerHTML = "";
  rightHead.style.width = `${timelineWidth}px`;
  for(let i=0;i<totalDays;i++){
    const ms = rangeStart + i*24*60*60*1000;
    const d = new Date(ms);
    const cell = document.createElement("div");
    cell.className = "daycell";
    cell.innerHTML = `<span class="d1">${weekdayRu(ms)}</span><span class="d2">${d.getDate()}</span>`;
    rightHead.appendChild(cell);
  }

  leftBody.innerHTML = "";
  rightBody.innerHTML = "";

  const markerLeftPx = Math.max(0, dayDiff(rangeStart, markerDeadlineMs)) * 44;

  rows.forEach(r=>{
    const ownerName = (r.executorIds && r.executorIds.length > 0)
      ? r.executorIds.map(id => state.participantsById.get(id)?.name || `#${id}`).join(", ")
      : "Вся команда";

    const leftRow = document.createElement("div");
    leftRow.className = "gantt-left-row";
    leftRow.innerHTML = `
      <div class="taskcell">
        <div>
          <div class="taskname">${escapeHtml(r.title)}</div>
          <div class="taskmeta">DL: ${new Date(r.deadline).toLocaleDateString()} • ${r.status===0 ? "Сделана" : "В процессе"}</div>
        </div>
      </div>
      <div class="ownercell">${escapeHtml(ownerName)}</div>
    `;
    leftBody.appendChild(leftRow);

    const rightRow = document.createElement("div");
    rightRow.className = "gantt-right-row";
    rightRow.style.width = `${timelineWidth}px`;

    const marker = document.createElement("div");
    marker.className = "deadline-marker";
    marker.style.left = `${markerLeftPx}px`;
    rightRow.appendChild(marker);

    const startPx = Math.max(0, dayDiff(rangeStart, r.start)) * 44;
    const durDays = Math.max(1, dayDiff(r.start, r.finish));
    const widthPx = Math.max(44, durDays * 44);

    const bar = document.createElement("div");
    const color = (r.status === 0) ? "green" : "blue";
    bar.className = `gantt-bar ${color}` + (r.recommended ? " recommended" : "");
    bar.style.left = `${startPx}px`;
    bar.style.width = `${widthPx}px`;
    bar.innerHTML = `<span class="bar-days">${daysText(r.finish - r.start)}</span>`;
    rightRow.appendChild(bar);

    rightBody.appendChild(rightRow);
  });
}

async function doRegister(e){
  e.preventDefault();
  try{
    const name = ($("regName")?.value ?? "").trim();
    const email = ($("regEmail")?.value ?? "").trim();
    const password = ($("regPassword")?.value ?? "").trim();
    const role = Number($("regRole")?.value ?? "0");
    const teamName = `Team ${name || "Student"}`;

    const res = await apiPost("/api/auth/register", {
      name,
      email,
      password,
      teamId: null,
      teamName,
      role
    });

    setAuth({
      token: res.token,
      userId: res.userId,
      participantId: res.participantId,
      role: res.role,
      email: res.email,
      name: res.name,
      teamId: res.teamId
    });

    setActiveTeam(res.teamId);
    setActiveProject(null);

    toast("Успешная регистрация");
    await refreshForStep("team");
    setStep("team");
  }catch(err){
    toast(String(err));
  }
}

async function doLogin(e){
  e.preventDefault();
  try{
    const email = ($("loginEmail")?.value ?? "").trim();
    const password = ($("loginPassword")?.value ?? "").trim();

    const res = await apiPost("/api/auth/login", { email, password });

    setAuth({
      token: res.token,
      userId: res.userId,
      participantId: res.participantId,
      role: res.role,
      email: res.email,
      name: res.name,
      teamId: res.teamId
    });

    setActiveTeam(res.teamId);
    setActiveProject(null);

    toast("Успешный вход");
    await refreshForStep("team");
    setStep("team");
  }catch(err){
    toast(String(err));
  }
}

function doLogout(){
  setAuth(null);
  setActiveTeam(null);
  setActiveProject(null);
  toast("Выход выполнен");
  refreshForStep("auth");
  setStep("auth");
}

async function initHandlers(){
  const saveBackendBtn = $("saveBackend");
  if(saveBackendBtn) saveBackendBtn.onclick = ()=> setBackend(($("backendUrl")?.value ?? "").trim());

  document.querySelectorAll(".step").forEach(btn=>{
    btn.onclick = async ()=>{
      const s = btn.dataset.step;
      await refreshForStep(s);
      setStep(s);
    };
  });

  const backToAuth = $("backToAuth");
  if(backToAuth) backToAuth.onclick = async ()=>{ await refreshForStep("auth"); setStep("auth"); };

  const backToTeam = $("backToTeam");
  if(backToTeam) backToTeam.onclick = async ()=>{ await refreshForStep("team"); setStep("team"); };

  const backToProject = $("backToProject");
  if(backToProject) backToProject.onclick = async ()=>{ await refreshForStep("project"); setStep("project"); };

  const backToPeople = $("backToPeople");
  if(backToPeople) backToPeople.onclick = async ()=>{ await refreshForStep("people"); setStep("people"); };

  const backToTasks = $("backToTasks");
  if(backToTasks) backToTasks.onclick = async ()=>{ await refreshForStep("tasks"); setStep("tasks"); };

  const toTeamFromAuth = $("toTeamFromAuth");
  if(toTeamFromAuth) toTeamFromAuth.onclick = async ()=>{ await refreshForStep("team"); setStep("team"); };

  const toProjectFromTeam = $("toProjectFromTeam");
  if(toProjectFromTeam) toProjectFromTeam.onclick = async ()=>{ await refreshForStep("project"); setStep("project"); };

  const toPeopleFromProject = $("toPeopleFromProject");
  if(toPeopleFromProject) toPeopleFromProject.onclick = async ()=>{ await refreshForStep("people"); setStep("people"); };

  const toTasksFromPeople = $("toTasksFromPeople");
  if(toTasksFromPeople) toTasksFromPeople.onclick = async ()=>{ await refreshForStep("tasks"); setStep("tasks"); };

  const toTimelineFromTasks = $("toTimelineFromTasks");
  if(toTimelineFromTasks) toTimelineFromTasks.onclick = async ()=>{ await refreshForStep("timeline"); setStep("timeline"); };

  const registerForm = $("registerForm");
  if(registerForm) registerForm.addEventListener("submit", doRegister);

  const loginForm = $("loginForm");
  if(loginForm) loginForm.addEventListener("submit", doLogin);

  const logoutBtn = $("logoutBtn");
  if(logoutBtn) logoutBtn.onclick = doLogout;

  const teamsReload = $("teamsReload");
  if(teamsReload) teamsReload.onclick = ()=>loadTeams();

  const projectsReload = $("projectsReload");
  if(projectsReload) projectsReload.onclick = ()=>loadProjects();

  const participantsReload = $("participantsReload");
  if(participantsReload) participantsReload.onclick = ()=>loadParticipants();

  const tasksReload = $("tasksReload");
  if(tasksReload) tasksReload.onclick = async ()=>{ await loadTasks(); await buildDepsUI(); await loadDependencies(); };

  const depsReload = $("depsReload");
  if(depsReload) depsReload.onclick = ()=>loadDependencies();

  const timelineReload = $("timelineReload");
  if(timelineReload) timelineReload.onclick = ()=>buildTimeline();

  const depCreateBtn = $("depCreateBtn");
  if(depCreateBtn) depCreateBtn.onclick = ()=>createDependencyFromPair();

  const depResetBtn = $("depResetBtn");
  if(depResetBtn) depResetBtn.onclick = ()=>resetDepPair();

  const teamForm = $("teamForm");
  if(teamForm){
    teamForm.addEventListener("submit", async (e)=>{
      e.preventDefault();
      try{
        if(!isCaptain()){ toast("Создание команды доступно капитану"); return; }
        const name = ($("teamName")?.value ?? "").trim();
        const created = await apiPost("/api/teams", { name });
        if($("teamName")) $("teamName").value = "";
        await loadTeams();
        if(created?.id) setActiveTeam(created.id);
        toast("Команда создана");
      }catch(err){
        toast(String(err));
      }
    });
  }

  const projectForm = $("projectForm");
  if(projectForm){
    projectForm.addEventListener("submit", async (e)=>{
      e.preventDefault();
      try{
        if(!isCaptain()){ toast("Создание проекта доступно капитану"); return; }
        const name = ($("projectName")?.value ?? "").trim();
        const subject = ($("projectSubject")?.value ?? "").trim();
        const teamId = Number($("projectTeamSelect")?.value ?? state.activeTeamId);
        const deadlineLocal = ($("projectDeadline")?.value ?? "").trim();
        const deadline = deadlineLocal ? localInputToIsoWithOffset(deadlineLocal) : null;

        const created = await apiPost("/api/projects", { name, subject, teamId, deadline });
        projectForm.reset();
        await loadProjects();
        if(created?.id) setActiveProject(created.id);
        toast("Проект создан");
      }catch(err){
        toast(String(err));
      }
    });
  }

  const participantForm = $("participantForm");
  if(participantForm){
    participantForm.addEventListener("submit", async (e)=>{
      e.preventDefault();
      try{
        if(!isCaptain()){ toast("Добавление участников доступно капитану"); return; }
        const name = ($("pName")?.value ?? "").trim();
        const email = (($("pEmail")?.value ?? "").trim() || null);
        const role = Number($("pRole")?.value ?? "0");
        const teamId = Number($("pTeamSelect")?.value ?? state.activeTeamId);

        await apiPost("/api/participants", { name, email, role, teamId });
        participantForm.reset();
        await loadParticipants();
        await renderExecutorsChecklist();
        toast("Участник создан");
      }catch(err){
        toast(String(err));
      }
    });
  }

  const taskForm = $("taskForm");
  if(taskForm){
    taskForm.addEventListener("submit", async (e)=>{
      e.preventDefault();
      try{
        if(!isCaptain()){ toast("Создание задач доступно капитану"); return; }
        const projectId = Number($("tProjectSelect")?.value ?? state.activeProjectId);
        const title = ($("tTitle")?.value ?? "").trim();
        const description = ($("tDesc")?.value ?? "").trim() || null;

        const startLocal = ($("tStart")?.value ?? "").trim();
        const endLocal = ($("tEnd")?.value ?? "").trim();
        const deadlineLocal = ($("tDeadline")?.value ?? "").trim();
        const durationDaysRaw = ($("tDurationDays")?.value ?? "").trim();
        const durationDays = durationDaysRaw ? Number(durationDaysRaw) : null;

        if(!deadlineLocal) throw new Error("Deadline обязателен");

        const hasStart = !!startLocal;
        const hasEnd = !!endLocal;
        if(hasStart !== hasEnd) throw new Error("Start и End должны быть либо оба заполнены, либо оба пустые");

        let startDate = null;
        let endDate = null;

        const deadlineIso = localInputToIsoWithOffset(deadlineLocal);

        if(hasStart && hasEnd){
          const sMs = new Date(startLocal).getTime();
          const eMs = new Date(endLocal).getTime();
          const dlMs = new Date(deadlineLocal).getTime();
          if(sMs > dlMs) throw new Error("Start не может быть позже Deadline");
          if(eMs > dlMs) throw new Error("End не может быть позже Deadline");
          if(eMs < sMs) throw new Error("End не может быть раньше Start");
          startDate = localInputToIsoWithOffset(startLocal);
          endDate = localInputToIsoWithOffset(endLocal);
        }else{
          if(!durationDays || durationDays < 1) throw new Error("Если даты пустые, нужно указать длительность (дни) >= 1");
        }

        const executorIds = getSelectedExecutorIds();

        await apiPost("/api/tasks", {
          projectId,
          title,
          description,
          startDate,
          endDate,
          deadline: deadlineIso,
          durationDays,
          executorIds
        });

        taskForm.reset();
        if(state.activeProjectId) $("tProjectSelect").value = String(state.activeProjectId);
        const proj = state.projectsById.get(state.activeProjectId);
        if(proj?.deadline && $("tDeadline")) $("tDeadline").value = isoToLocalInput(proj.deadline);

        await loadTasks();
        await buildDepsUI();
        await loadDependencies();
        toast("Задача создана");
      }catch(err){
        toast(String(err));
      }
    });
  }
}

(function init(){
  const backendInput = $("backendUrl");
  if(backendInput) backendInput.value = getBackend();

  setActiveTeam(state.activeTeamId);
  setActiveProject(state.activeProjectId);

  initHandlers().then(async ()=>{
    renderAuthState();
    applyRoleGates();
    await refreshForStep(state.step);
    setStep(state.step);
  }).catch(e=>toast(String(e)));
})();
