/* ============================================================
 * Luminous · 微光 —— 轻量化横屏 2D 像素游戏
 * 场景：登录界面（DOM）→ 主场景「荒原·初始之地」（Canvas）
 * 素材：Kenney Tiny Town (CC0) / 主角为自有图片
 * ============================================================ */
'use strict';

/* ---------------- 配置 ---------------- */
const CONFIG = {
  VIEW_W: 640,          // 画布内部分辨率（CSS 再放大）
  VIEW_H: 360,
  SCALE: 2,             // 世界像素 → 画布像素 的缩放（16px瓦片显示为32px）
  TILE: 16,             // 图集瓦片边长
  MAP_W: 64,            // 世界宽（瓦片）
  MAP_H: 48,            // 世界高（瓦片）
  PLAYER_SPEED: 58,     // 世界像素 / 秒
  PLAYER_H: 20,         // 主角显示高度（世界像素）
  WALK_FRAME_TIME: 0.16,
  BGM_VOLUME: 0.5,
};

/* 图集索引（Tiny Town packed，12 列） */
const ATLAS_COLS = 12;
const T = (c, r) => ({ c, r });
const TILES = {
  dirtPlain: T(3,3),   // 纯荒地
  dirtPlain2:T(1,2),   // 纯荒地（略有色差）
  dirtTuft1: T(4,3),   // 带零星杂草
  dirtTuft2: T(5,3),
  dirtTuft3: T(6,3),
  treeFallT: T(3,0), treeFallB: T(3,1),   // 秋树（上/下）
  treePineT: T(4,0), treePineB: T(4,1),   // 绿树（上/下）
  bush:      T(5,0),
  mushroom:  T(5,2),
  rock:      T(9,8),
  well:      T(8,8),
  hay:       T(8,7),
  sign:      T(11,6),
  fenceH:    T(9,6),
  fenceV:    T(11,4),
  // 木屋：图集 (0..3, 4..7)
  house: (dx, dy) => T(dx, 4 + dy),
};

/* ---------------- 资源加载 ---------------- */
const Assets = { images: {}, audio: {}, ready: false };

function loadImage(key, src) {
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => { Assets.images[key] = img; resolve(); };
    img.onerror = () => { console.warn('图片加载失败:', src); resolve(); };
    img.src = src;
  });
}

function loadAll() {
  const jobs = [
    loadImage('tilemap', 'assets/tiles/tilemap.png'),
    loadImage('heroIdle', 'assets/sprites/hero_0.png'),
    loadImage('heroWalk', 'assets/sprites/hero_1.png'),
  ];
  // 音频不存在也不阻塞游戏
  Assets.audio.login = new Audio('assets/audio/login.ogg');
  Assets.audio.town  = new Audio('assets/audio/town.ogg');
  for (const k of ['login', 'town']) {
    Assets.audio[k].loop = true;
    Assets.audio[k].volume = CONFIG.BGM_VOLUME;
  }
  return Promise.all(jobs).then(() => { Assets.ready = true; });
}

let muted = false;
function playBGM(key) {
  for (const k of ['login', 'town']) {
    const a = Assets.audio[k];
    if (k === key && !muted) { a.play().catch(() => {}); }
    else a.pause();
  }
}

/* ---------------- 伪随机（固定种子，地图可复现） ---------------- */
function mulberry32(seed) {
  return function () {
    seed |= 0; seed = (seed + 0x6D2B79F5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const hash2 = (x, y) => {
  let h = (x * 374761393 + y * 668265263) ^ 0x5bf03635;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
};

/* ---------------- 地图 ---------------- */
/* 每层一格存放 {c,r} 或 null；solid 为布尔矩阵 */
const GameMap = {
  ground: [], deco: [], solid: [],
  housePos: { x: 30, y: 10 },
  build() {
    const { MAP_W: W, MAP_H: H } = CONFIG;
    const rnd = mulberry32(20261012);
    this.ground = Array.from({ length: H }, () => new Array(W).fill(null));
    this.deco   = Array.from({ length: H }, () => new Array(W).fill(null));
    this.solid  = Array.from({ length: H }, () => new Array(W).fill(false));

    // 地面：纯荒地为主（约 82%），零星杂草地块点缀
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const h = hash2(x, y);
        let t;
        if (h < 0.75) t = TILES.dirtPlain;
        else if (h < 0.82) t = TILES.dirtPlain2;
        else if (h < 0.88) t = TILES.dirtTuft1;
        else if (h < 0.94) t = TILES.dirtTuft2;
        else t = TILES.dirtTuft3;
        this.ground[y][x] = t;
      }
    }

    const reserved = (x, y) =>
      (x >= this.housePos.x - 3 && x <= this.housePos.x + 7 &&
       y >= this.housePos.y - 2 && y <= this.housePos.y + 9);

    // 散落的装饰：树 / 灌木 / 蘑菇 / 石头
    for (let i = 0; i < 90; i++) {
      const x = Math.floor(rnd() * W), y = Math.floor(rnd() * (H - 1));
      if (reserved(x, y)) continue;
      const roll = rnd();
      if (roll < 0.30 && !this.deco[y][x] && !this.deco[y + 1][x]) {
        const fall = rnd() < 0.5;
        this.deco[y][x]     = fall ? TILES.treeFallT : TILES.treePineT;
        this.deco[y + 1][x] = fall ? TILES.treeFallB : TILES.treePineB;
        this.solid[y + 1][x] = true;
      } else if (roll < 0.5 && !this.deco[y][x]) {
        this.deco[y][x] = TILES.bush;
      } else if (roll < 0.65 && !this.deco[y][x]) {
        this.deco[y][x] = TILES.mushroom;
      } else if (roll < 0.75 && !this.deco[y][x]) {
        this.deco[y][x] = TILES.rock;
        this.solid[y][x] = true;
      }
    }

    this.buildHouse();
    this.buildYard();
  },
  buildHouse() {
    const { x: hx, y: hy } = this.housePos;
    for (let dy = 0; dy < 4; dy++) {
      for (let dx = 0; dx < 4; dx++) {
        this.deco[hy + dy][hx + dx] = TILES.house(dx, dy);
        this.solid[hy + dy][hx + dx] = true;
      }
    }
  },
  buildYard() {
    const { x: hx, y: hy } = this.housePos;
    // 栅栏院子（房子前方），门口留两格缺口
    for (let dx = -2; dx <= 6; dx++) {
      if (dx === 1 || dx === 2) continue;
      this.deco[hy + 7][hx + dx] = TILES.fenceH;
      this.solid[hy + 7][hx + dx] = true;
    }
    for (let dy = 5; dy <= 6; dy++) {
      this.deco[hy + dy][hx - 2] = TILES.fenceV;
      this.solid[hy + dy][hx - 2] = true;
      this.deco[hy + dy][hx + 6] = TILES.fenceV;
      this.solid[hy + dy][hx + 6] = true;
    }
    // 水井、干草堆、木牌
    this.deco[hy + 5][hx + 5] = TILES.well;
    this.solid[hy + 5][hx + 5] = true;
    this.deco[hy + 6][hx - 1] = TILES.hay;
    this.solid[hy + 6][hx - 1] = true;
    this.deco[hy + 6][hx + 3] = TILES.sign;
    this.solid[hy + 6][hx + 3] = true;
  },
};

/* ---------------- 玩家 ---------------- */
const Player = {
  x: 0, y: 0,           // 世界像素（脚底锚点）
  vx: 0, vy: 0,
  faceLeft: false,
  moving: false,
  animT: 0,
  name: '旅人',
  spawn() {
    const { x: hx, y: hy } = GameMap.housePos;
    // 出生在房门口，保证整座小屋在视野内
    this.x = (hx + 1.5) * CONFIG.TILE;
    this.y = (hy + 5.2) * CONFIG.TILE;
  },
  // 脚底碰撞盒
  box(nx, ny) {
    return { l: nx - 5, r: nx + 5, t: ny - 7, b: ny - 1 };
  },
  collide(nx, ny) {
    const b = this.box(nx, ny);
    const { TILE, MAP_W, MAP_H } = CONFIG;
    const x0 = Math.floor(b.l / TILE), x1 = Math.floor(b.r / TILE);
    const y0 = Math.floor(b.t / TILE), y1 = Math.floor(b.b / TILE);
    for (let y = y0; y <= y1; y++) {
      for (let x = x0; x <= x1; x++) {
        if (x < 0 || y < 0 || x >= MAP_W || y >= MAP_H) return true;
        if (GameMap.solid[y][x]) return true;
      }
    }
    return false;
  },
  update(dt, input) {
    let dx = input.x, dy = input.y;
    const len = Math.hypot(dx, dy);
    if (len > 1) { dx /= len; dy /= len; }
    this.moving = len > 0.01;
    if (this.moving) {
      if (dx < -0.01) this.faceLeft = true;
      else if (dx > 0.01) this.faceLeft = false;
      this.animT += dt;
      const sp = CONFIG.PLAYER_SPEED * dt;
      const nx = this.x + dx * sp;
      if (!this.collide(nx, this.y)) this.x = nx;
      const ny = this.y + dy * sp;
      if (!this.collide(this.x, ny)) this.y = ny;
    } else {
      this.animT = 0;
    }
  },
  draw(ctx) {
    const img = (this.moving && Math.floor(this.animT / CONFIG.WALK_FRAME_TIME) % 2 === 1)
      ? Assets.images.heroWalk : Assets.images.heroIdle;
    if (!img) return;
    const h = CONFIG.PLAYER_H;
    const w = h * (img.width / img.height);
    ctx.save();
    ctx.translate(Math.round(this.x), Math.round(this.y));
    if (this.faceLeft) ctx.scale(-1, 1);
    // 影子
    ctx.fillStyle = 'rgba(0,0,0,.25)';
    ctx.beginPath();
    ctx.ellipse(0, -1, w * 0.32, 2.4, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.drawImage(img, -w / 2, -h, w, h);
    ctx.restore();
  },
};

/* ---------------- 输入（键盘 + 虚拟摇杆） ---------------- */
const Input = {
  keys: {},
  joy: { active: false, id: -1, ox: 0, oy: 0, x: 0, y: 0 },
  get x() {
    let v = 0;
    if (this.keys['ArrowLeft'] || this.keys['a']) v -= 1;
    if (this.keys['ArrowRight'] || this.keys['d']) v += 1;
    if (this.joy.active) v += this.joy.x;
    return Math.max(-1, Math.min(1, v));
  },
  get y() {
    let v = 0;
    if (this.keys['ArrowUp'] || this.keys['w']) v -= 1;
    if (this.keys['ArrowDown'] || this.keys['s']) v += 1;
    if (this.joy.active) v += this.joy.y;
    return Math.max(-1, Math.min(1, v));
  },
  init(canvas) {
    window.addEventListener('keydown', (e) => {
      const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
      this.keys[k] = true;
      if (['ArrowUp','ArrowDown','ArrowLeft','ArrowRight',' '].includes(e.key)) e.preventDefault();
    });
    window.addEventListener('keyup', (e) => {
      const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
      this.keys[k] = false;
    });

    const R = 40; // 摇杆半径（画布像素）
    const toCanvas = (e) => {
      const rect = canvas.getBoundingClientRect();
      return {
        x: (e.clientX - rect.left) * (canvas.width / rect.width),
        y: (e.clientY - rect.top) * (canvas.height / rect.height),
      };
    };
    canvas.addEventListener('pointerdown', (e) => {
      const p = toCanvas(e);
      // 左半屏触发摇杆
      if (p.x < canvas.width * 0.5 && !this.joy.active) {
        this.joy.active = true;
        this.joy.id = e.pointerId;
        this.joy.ox = p.x; this.joy.oy = p.y;
        this.joy.x = 0; this.joy.y = 0;
        canvas.setPointerCapture(e.pointerId);
      }
    });
    canvas.addEventListener('pointermove', (e) => {
      if (!this.joy.active || e.pointerId !== this.joy.id) return;
      const p = toCanvas(e);
      let dx = (p.x - this.joy.ox) / R, dy = (p.y - this.joy.oy) / R;
      const len = Math.hypot(dx, dy);
      if (len > 1) { dx /= len; dy /= len; }
      this.joy.x = dx; this.joy.y = dy;
    });
    const end = (e) => {
      if (e.pointerId === this.joy.id) {
        this.joy.active = false; this.joy.x = 0; this.joy.y = 0;
      }
    };
    canvas.addEventListener('pointerup', end);
    canvas.addEventListener('pointercancel', end);
  },
  drawJoystick(ctx) {
    if (!this.joy.active) return;
    ctx.save();
    ctx.globalAlpha = 0.35;
    ctx.fillStyle = '#fff';
    ctx.beginPath();
    ctx.arc(this.joy.ox, this.joy.oy, 30, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = 0.7;
    ctx.beginPath();
    ctx.arc(this.joy.ox + this.joy.x * 22, this.joy.oy + this.joy.y * 22, 13, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  },
};

/* ---------------- 相机 ---------------- */
const Camera = {
  x: 0, y: 0,
  follow(target) {
    const vw = CONFIG.VIEW_W / CONFIG.SCALE;
    const vh = CONFIG.VIEW_H / CONFIG.SCALE;
    this.x = target.x - vw / 2;
    this.y = target.y - vh / 2;
    const maxX = CONFIG.MAP_W * CONFIG.TILE - vw;
    const maxY = CONFIG.MAP_H * CONFIG.TILE - vh;
    this.x = Math.max(0, Math.min(maxX, this.x));
    this.y = Math.max(0, Math.min(maxY, this.y));
  },
};

/* ---------------- 渲染 ---------------- */
function drawTile(ctx, atlas, t, wx, wy) {
  if (!t || !atlas) return;
  ctx.drawImage(
    atlas,
    t.c * CONFIG.TILE, t.r * CONFIG.TILE, CONFIG.TILE, CONFIG.TILE,
    wx, wy, CONFIG.TILE, CONFIG.TILE
  );
}

function render(ctx) {
  const atlas = Assets.images.tilemap;
  ctx.imageSmoothingEnabled = false;
  ctx.clearRect(0, 0, CONFIG.VIEW_W, CONFIG.VIEW_H);

  ctx.save();
  ctx.scale(CONFIG.SCALE, CONFIG.SCALE);
  ctx.translate(-Math.floor(Camera.x), -Math.floor(Camera.y));

  // 只画可视范围
  const T16 = CONFIG.TILE;
  const x0 = Math.max(0, Math.floor(Camera.x / T16));
  const y0 = Math.max(0, Math.floor(Camera.y / T16));
  const x1 = Math.min(CONFIG.MAP_W - 1, Math.ceil((Camera.x + CONFIG.VIEW_W / CONFIG.SCALE) / T16));
  const y1 = Math.min(CONFIG.MAP_H - 1, Math.ceil((Camera.y + CONFIG.VIEW_H / CONFIG.SCALE) / T16));

  for (let y = y0; y <= y1; y++)
    for (let x = x0; x <= x1; x++)
      drawTile(ctx, atlas, GameMap.ground[y][x], x * T16, y * T16);

  // 装饰 + 玩家按 Y 排序（伪深度）
  const drawList = [];
  for (let y = y0; y <= y1; y++)
    for (let x = x0; x <= x1; x++)
      if (GameMap.deco[y][x])
        drawList.push({ y: (y + 1) * T16, draw: (c) => drawTile(c, atlas, GameMap.deco[y][x], x * T16, y * T16) });
  drawList.push({ y: Player.y, draw: (c) => Player.draw(c) });
  drawList.sort((a, b) => a.y - b.y);
  for (const item of drawList) item.draw(ctx);

  ctx.restore();

  // HUD
  ctx.fillStyle = 'rgba(13,10,20,.55)';
  ctx.fillRect(6, 6, 150, 34);
  ctx.fillStyle = '#f5c542';
  ctx.font = '9px "PressStart2P", monospace';
  ctx.fillText('荒原 · 初始之地', 12, 19);
  ctx.fillStyle = '#efe6ff';
  ctx.font = '10px "Microsoft YaHei", sans-serif';
  ctx.fillText(`旅人：${Player.name}`, 12, 34);

  // 操作提示（进入后显示几秒）
  if (hintT > 0) {
    ctx.fillStyle = 'rgba(13,10,20,.55)';
    const w = 210;
    ctx.fillRect((CONFIG.VIEW_W - w) / 2, CONFIG.VIEW_H - 26, w, 18);
    ctx.fillStyle = '#b9a8e0';
    ctx.font = '10px "Microsoft YaHei", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('WASD / 方向键移动 · 触屏拖左半屏', CONFIG.VIEW_W / 2, CONFIG.VIEW_H - 13);
    ctx.textAlign = 'left';
  }

  Input.drawJoystick(ctx);
}

/* ---------------- 画布自适应 ---------------- */
function fitCanvas(canvas) {
  const aw = window.innerWidth, ah = window.innerHeight;
  const ratio = CONFIG.VIEW_W / CONFIG.VIEW_H;
  let w = aw, h = aw / ratio;
  if (h > ah) { h = ah; w = ah * ratio; }
  canvas.style.width = `${w}px`;
  canvas.style.height = `${h}px`;
}

/* ---------------- 主流程 ---------------- */
let hintT = 0;
let lastTs = 0;

function loop(ts, ctx) {
  const dt = Math.min(0.05, (ts - lastTs) / 1000 || 0);
  lastTs = ts;
  Player.update(dt, Input);
  Camera.follow(Player);
  if (hintT > 0) hintT -= dt;
  render(ctx);
  requestAnimationFrame((t) => loop(t, ctx));
}

function buildStars() {
  const box = document.getElementById('stars');
  for (let i = 0; i < 40; i++) {
    const s = document.createElement('i');
    s.style.left = `${Math.random() * 100}%`;
    s.style.top = `${Math.random() * 100}%`;
    s.style.animationDelay = `${Math.random() * 2.4}s`;
    box.appendChild(s);
  }
}

function addMuteButton() {
  const btn = document.createElement('button');
  btn.textContent = '🔊';
  btn.style.cssText = `position:absolute;top:8px;right:10px;z-index:20;
    background:rgba(13,10,20,.55);border:2px solid #553a7a;color:#fff;
    font-size:14px;padding:4px 8px;cursor:pointer;border-radius:4px;`;
  btn.onclick = () => {
    muted = !muted;
    btn.textContent = muted ? '🔇' : '🔊';
    for (const k of ['login', 'town']) Assets.audio[k].muted = muted;
  };
  document.body.appendChild(btn);
}

async function main() {
  const canvas = document.getElementById('game');
  const ctx = canvas.getContext('2d');
  fitCanvas(canvas);
  window.addEventListener('resize', () => fitCanvas(canvas));
  buildStars();
  addMuteButton();

  // 首次交互解锁音频（浏览器自动播放限制）
  const unlock = () => { playBGM('login'); window.removeEventListener('pointerdown', unlock); };
  window.addEventListener('pointerdown', unlock);

  await loadAll();
  GameMap.build();
  Input.init(canvas);

  // 登录逻辑
  const loginEl = document.getElementById('login');
  const inputEl = document.getElementById('nickname');
  inputEl.value = localStorage.getItem('luminous_name') || '';
  document.getElementById('btnStart').addEventListener('click', () => {
    const name = inputEl.value.trim() || '无名旅人';
    Player.name = name;
    localStorage.setItem('luminous_name', name);
    Player.spawn();
    Camera.follow(Player);
    loginEl.classList.add('hidden');
    playBGM('town');
    hintT = 6;
    lastTs = performance.now();
    requestAnimationFrame((t) => loop(t, ctx));
  });
}

main();
