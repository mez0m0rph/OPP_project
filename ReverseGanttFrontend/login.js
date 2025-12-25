function $(id){ return document.getElementById(id); }

function toast(msg){
  const t = $("toast");
  t.textContent = msg;
  t.classList.remove("hidden");
  clearTimeout(window.__toastTimer);
  window.__toastTimer = setTimeout(()=>t.classList.add("hidden"), 2600);
}

function getBackend(){
  return localStorage.getItem("BACKEND_URL") || $("backendUrl").value.trim() || "http://localhost:5051";
}
function setBackend(url){
  localStorage.setItem("BACKEND_URL", url);
  $("backendUrl").value = url;
  toast("Backend сохранён: " + url);
}

async function apiGet(path){
  const url = `${getBackend()}${path}`;
  const res = await fetch(url);
  if(!res.ok) throw new Error(await res.text());
  const ct = res.headers.get("content-type") || "";
  if(ct.includes("application/json")) return await res.json();
  return null;
}

async function apiPost(path, body){
  const url = `${getBackend()}${path}`;
  const res = await fetch(url, {
    method:"POST",
    headers: {"Content-Type":"application/json"},
    body: JSON.stringify(body)
  });
  if(!res.ok) throw new Error(await res.text());
  const ct = res.headers.get("content-type") || "";
  if(ct.includes("application/json")) return await res.json();
  return null;
}

function saveAuth(auth){
  localStorage.setItem("TOKEN", auth.token);
  localStorage.setItem("ROLE", auth.role);
  localStorage.setItem("PARTICIPANT_ID", String(auth.participantId));
  localStorage.setItem("TEAM_ID", String(auth.teamId));
  localStorage.setItem("EMAIL", auth.email);
  localStorage.setItem("NAME", auth.name);
}

async function fillTeams(){
  const sel = $("regTeamSelect");
  sel.innerHTML = "";
  try{
    const teams = await apiGet("/api/teams/public");
    const opt0 = document.createElement("option");
    opt0.value = "";
    opt0.textContent = "— выбрать —";
    sel.appendChild(opt0);

    (teams || []).forEach(t=>{
      const opt = document.createElement("option");
      opt.value = String(t.id);
      opt.textContent = `#${t.id} ${t.name}`;
      sel.appendChild(opt);
    });
  }catch{
    const opt0 = document.createElement("option");
    opt0.value = "";
    opt0.textContent = "— команды недоступны —";
    sel.appendChild(opt0);
  }
}

$("saveBackend").onclick = ()=> setBackend($("backendUrl").value.trim());

$("loginForm").addEventListener("submit", async (e)=>{
  e.preventDefault();
  try{
    const email = $("loginEmail").value.trim();
    const password = $("loginPassword").value;
    const auth = await apiPost("/api/auth/login", { email, password });
    saveAuth(auth);
    window.location.href = "index.html";
  }catch(err){ toast(String(err)); }
});

$("regForm").addEventListener("submit", async (e)=>{
  e.preventDefault();
  try{
    const name = $("regName").value.trim();
    const email = $("regEmail").value.trim();
    const password = $("regPassword").value;
    const role = Number($("regRole").value);
    const teamIdRaw = $("regTeamSelect").value;
    const teamId = teamIdRaw ? Number(teamIdRaw) : null;
    const teamName = $("regTeamName").value.trim() || null;

    const auth = await apiPost("/api/auth/register", { name, email, password, teamId, teamName, role });
    saveAuth(auth);
    window.location.href = "index.html";
  }catch(err){ toast(String(err)); }
});

(function init(){
  $("backendUrl").value = getBackend();
  $("saveBackend").click();
  fillTeams();
})();
