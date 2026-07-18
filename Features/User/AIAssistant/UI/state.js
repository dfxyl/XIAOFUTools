// AI 助手前端状态与 DOM 引用
// 由 tools/maintenance/split-ai-script.cjs 从原单文件按职责拆分。

const DANGEROUS_PATTERNS = [
    // 文件删除操作
    { pattern: /os\.remove\s*\(/i, desc: '删除文件 (os.remove)' },
    { pattern: /os\.rmdir\s*\(/i, desc: '删除目录 (os.rmdir)' },
    { pattern: /os\.unlink\s*\(/i, desc: '删除文件 (os.unlink)' },
    { pattern: /shutil\.rmtree\s*\(/i, desc: '递归删除目录 (shutil.rmtree)' },
    { pattern: /shutil\.move\s*\(/i, desc: '移动/重命名文件 (shutil.move)' },
    // ArcPy危险操作
    { pattern: /arcpy\.Delete_management\s*\(/i, desc: '删除要素/数据 (Delete_management)' },
    { pattern: /arcpy\.DeleteFeatures_management\s*\(/i, desc: '删除要素 (DeleteFeatures)' },
    { pattern: /arcpy\.DeleteRows_management\s*\(/i, desc: '删除行 (DeleteRows)' },
    { pattern: /arcpy\.Truncate\s*\(/i, desc: '截断表 (Truncate)' },
    { pattern: /arcpy\.TruncateTable_management\s*\(/i, desc: '截断表 (TruncateTable)' },
    { pattern: /\.deleteRow\s*\(/i, desc: '删除游标行 (deleteRow)' },
    // 系统命令
    { pattern: /os\.system\s*\(/i, desc: '执行系统命令 (os.system)' },
    { pattern: /subprocess\.(run|call|Popen)\s*\(/i, desc: '执行子进程 (subprocess)' },
    // 数据库危险操作
    { pattern: /\bDROP\s+(TABLE|DATABASE|INDEX)/i, desc: 'SQL删除操作 (DROP)' },
    { pattern: /\bDELETE\s+FROM\b/i, desc: 'SQL删除数据 (DELETE)' },
    { pattern: /\bTRUNCATE\s+TABLE\b/i, desc: 'SQL截断表 (TRUNCATE)' },
    // 文件覆盖写入
    { pattern: /open\s*\([^)]*['"][wa]['"][^)]*\)/i, desc: '写入/覆盖文件 (open write)' },
];

const chatList = document.getElementById('chatList');

const inputText = document.getElementById('input-text');

const modelSelect = document.getElementById('modelSelect');

const historyBtn = document.getElementById('historyBtn');

const historyPanel = document.getElementById('historyPanel');

const historyList = document.getElementById('historyList');

const closeHistoryBtn = document.getElementById('closeHistoryBtn');

const settingsBtn = document.getElementById('settingsBtn');

const newBtn = document.getElementById('newBtn');

const sendBtn = document.getElementById('sendBtn');

const modeToggle = document.getElementById('modeToggle');

const confirmDialog = document.getElementById('confirmDialog');

const confirmDialogSessionName = document.getElementById('confirmDialogSessionName');

const confirmDialogCancel = document.getElementById('confirmDialogCancel');

const confirmDialogConfirm = document.getElementById('confirmDialogConfirm');

const toolApprovalDialog = document.getElementById('toolApprovalDialog');

const toolApprovalBody = document.getElementById('toolApprovalBody');

const toolApprovalCancelBtn = document.getElementById('toolApprovalCancelBtn');

const toolApprovalAllowOnceBtn = document.getElementById('toolApprovalAllowOnceBtn');

const toolApprovalAllowBtn = document.getElementById('toolApprovalAllowBtn');

const imageUploadBtn = document.getElementById('imageUploadBtn');

const imageInput = document.getElementById('imageInput');

const imagePreviewContainer = document.getElementById('imagePreviewContainer');

const scrollToBottomBtn = document.getElementById('scrollToBottomBtn');

let isStreaming = false;

let currentAiMsgContent = null;

let currentSessionId = null;

let currentStreamId = null;

let pendingDeleteSession = null;

let currentMode = 'chat';

let currentStreamText = '';

let currentTurnId = null;

const turnMessageMap = new Map();

const turnTextMap = new Map();

const activeSessionStreams = new Map();

let selectedImages = [];

let currentModelSupportsVision = false;

let pythonExecutionHistory = new Map();

let pendingToolApprovalRequestId = null;

let scrollTimeout;
