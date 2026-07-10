import { useState, useEffect, useRef, useCallback } from 'react';
import axios from 'axios';
import Header from './components/Header';
import ConfirmModal from './components/ConfirmModal';
import Sidebar from './components/Sidebar';
import UploadPanel from './components/UploadPanel';
import ServerManager from './components/ServerManager';
import PreviewPanel from './components/PreviewPanel';
import AccessLogin from './components/AccessLogin';
import AccessManager from './components/AccessManager';

const API_BASE_URL = 'http://localhost:5230/api/ftp';
const ACCESS_API_BASE_URL = 'http://localhost:5230/api/access';

function App() {
  // Navigation State
  const [activeView, setActiveView] = useState('explorer'); // 'explorer' or 'servers'
  const [appToken, setAppToken] = useState(() => localStorage.getItem('ftpManagerToken') || '');
  const [currentUser, setCurrentUser] = useState(() => {
    const saved = localStorage.getItem('ftpManagerUser');
    return saved ? JSON.parse(saved) : null;
  });
  const [loginForm, setLoginForm] = useState({ username: '', password: '' });

  // Access management state
  const [accessTab, setAccessTab] = useState('users');
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [permissions, setPermissions] = useState([]);
  const [editingUserId, setEditingUserId] = useState(null);
  const [editingRoleId, setEditingRoleId] = useState(null);
  const [userForm, setUserForm] = useState({ fullName: '', username: '', password: '', roleId: '', isActive: true });
  const [roleForm, setRoleForm] = useState({ name: '', description: '', permissions: [] });

  // Dynamic FTP Servers State
  const [ftpServers, setFtpServers] = useState([]);
  const [selectedServerId, setSelectedServerId] = useState('default');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  // New FTP Server form states
  const [newServerName, setNewServerName] = useState('');
  const [newServerHost, setNewServerHost] = useState('127.0.0.1');
  const [newServerPort, setNewServerPort] = useState('');
  const [newServerUser, setNewServerUser] = useState('');
  const [newServerPass, setNewServerPass] = useState('');

  // File tree flat-state
  const [folderData, setFolderData] = useState({ '/': [] });
  const [expandedFolders, setExpandedFolders] = useState({ '/': true });
  const [selectedPath, setSelectedPath] = useState('/');
  const [searchQuery, setSearchQuery] = useState('');
  const [loading, setLoading] = useState(false);

  // File upload state
  const [selectedFile, setSelectedFile] = useState(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const fileInputRef = useRef(null);

  // Logs state
  const [activeLogTab, setActiveLogTab] = useState('file'); // 'file' or 'database'
  const [logs, setLogs] = useState([]);
  const [expandedLogId, setExpandedLogId] = useState(null);

  // Toast / Status state
  const [notification, setNotification] = useState(null);

  // Preview states
  const [previewFile, setPreviewFile] = useState(null);
  const [previewData, setPreviewData] = useState(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  // Chunked Upload progress states
  const [uploadProgress, setUploadProgress] = useState(null);
  const [uploadStatus, setUploadStatus] = useState('');

  // Double Confirmation Modal State
  const [confirmModal, setConfirmModal] = useState({
    isOpen: false,
    title: '',
    message: '',
    step: 1, // 1 or 2
    onConfirm: null
  });

  const requestDoubleConfirm = (title, message, onConfirm) => {
    setConfirmModal({
      isOpen: true,
      title: title,
      message: message,
      step: 1,
      onConfirm: onConfirm
    });
  };

  const showToast = (message, type = 'success') => {
    setNotification({ message, type });
    setTimeout(() => setNotification(null), 4000);
  };

  const hasPermission = useCallback((permission) => {
    return currentUser?.permissions?.includes(permission);
  }, [currentUser]);

  useEffect(() => {
    if (appToken) {
      axios.defaults.headers.common.Authorization = `Bearer ${appToken}`;
    } else {
      delete axios.defaults.headers.common.Authorization;
    }
  }, [appToken]);

  const handleAppLogin = async (e) => {
    e.preventDefault();
    if (!loginForm.username || !loginForm.password) {
      showToast('Lutfen kullanici adi ve sifre girin.', 'error');
      return;
    }

    setLoading(true);
    try {
      const response = await axios.post(`${ACCESS_API_BASE_URL}/login`, loginForm);
      setAppToken(response.data.token);
      setCurrentUser(response.data.user);
      localStorage.setItem('ftpManagerToken', response.data.token);
      localStorage.setItem('ftpManagerUser', JSON.stringify(response.data.user));
      setLoginForm({ username: '', password: '' });
      showToast('Uygulama girisi basarili.');
    } catch (error) {
      showToast(`Giris basarisiz: ${error.response?.data || error.message}`, 'error');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    setAppToken('');
    setCurrentUser(null);
    setIsLoggedIn(false);
    setActiveView('explorer');
    localStorage.removeItem('ftpManagerToken');
    localStorage.removeItem('ftpManagerUser');
  };

  // Fetch servers from backend
  const fetchFtpServers = useCallback(async () => {
    if (!appToken || !hasPermission('servers.view')) return;
    try {
      const response = await axios.get(`${API_BASE_URL}/servers`);
      setFtpServers(response.data);
      
      // If current selected server is no longer in list, fallback to default
      if (!response.data.some(s => s.id === selectedServerId)) {
        setSelectedServerId('default');
        setUsername('');
        setPassword('');
        setIsLoggedIn(false);
      }
    } catch (error) {
      console.error('FTP sunucuları yüklenemedi:', error);
      showToast('FTP sunucu listesi çekilemedi.', 'error');
    }
  }, [appToken, hasPermission, selectedServerId]);

  // Fetch logs from backend
  const fetchLogs = useCallback(async (tab) => {
    if (!appToken || !hasPermission('logs.view')) {
      setLogs([]);
      return;
    }
    try {
      const endpoint = tab === 'database' ? 'logs/database' : 'logs/file';
      const response = await axios.get(`${API_BASE_URL}/${endpoint}`);
      setLogs(response.data);
    } catch (error) {
      console.error('Loglar çekilemedi:', error);
    }
  }, [appToken, hasPermission]);

  const fetchAccessData = useCallback(async () => {
    if (!appToken || !hasPermission('access.manage')) return;
    const [usersResponse, rolesResponse, permissionsResponse] = await Promise.all([
      axios.get(`${ACCESS_API_BASE_URL}/users`),
      axios.get(`${ACCESS_API_BASE_URL}/roles`),
      axios.get(`${ACCESS_API_BASE_URL}/permissions`)
    ]);
    setUsers(usersResponse.data);
    setRoles(rolesResponse.data);
    setPermissions(permissionsResponse.data);
    setUserForm((prev) => ({ ...prev, roleId: prev.roleId || rolesResponse.data[0]?.id || '' }));
  }, [appToken, hasPermission]);

  const refreshCurrentUser = useCallback(async () => {
    if (!appToken) return;
    try {
      const response = await axios.get(`${ACCESS_API_BASE_URL}/me`);
      setCurrentUser(response.data);
      localStorage.setItem('ftpManagerUser', JSON.stringify(response.data));
    } catch {
      handleLogout();
    }
  }, [appToken]);

  // Helper to add logs
  const addLog = useCallback((message, type = 'INFO', isDbLog = false) => {
    console.log(`[${type}] [DB: ${isDbLog}] ${message}`);
    fetchLogs(activeLogTab);
  }, [activeLogTab, fetchLogs]);

  // Fetch directory listing
  const fetchFolder = useCallback(async (path) => {
    setLoading(true);
    try {
      const response = await axios.get(`${API_BASE_URL}/list?path=${encodeURIComponent(path)}`, {
        headers: { 
          'X-FTP-Server-Id': selectedServerId,
          'X-FTP-Username': username,
          'X-FTP-Password': password
        }
      });
      setFolderData(prev => ({
        ...prev,
        [path]: response.data
      }));
      addLog(`"${path}" dizini başarıyla listelendi.`, 'INFO', false);
    } catch (error) {
      console.error(error);
      showToast(`Dizin listelenirken hata oluştu: ${error.response?.data || error.message}`, 'error');
      addLog(`Dizin listeleme hatası ("${path}"): ${error.message}`, 'ERROR', true);
    } finally {
      setLoading(false);
    }
  }, [addLog, selectedServerId, username, password]);

  useEffect(() => {
    if (!appToken) return;
    const timer = setTimeout(() => {
      refreshCurrentUser();
    }, 0);
    return () => clearTimeout(timer);
  }, [appToken, refreshCurrentUser]);

  // Fetch initial logs and servers on mount
  useEffect(() => {
    if (!appToken) return;
    const timer = setTimeout(() => {
      fetchFtpServers();
    }, 0);
    return () => clearTimeout(timer);
  }, [appToken, fetchFtpServers]);

  useEffect(() => {
    if (activeView !== 'access') return;
    const timer = setTimeout(() => {
      fetchAccessData().catch((error) => showToast(`Yetki verileri alinamadi: ${error.response?.data || error.message}`, 'error'));
    }, 0);
    return () => clearTimeout(timer);
  }, [activeView, fetchAccessData]);

  // Fetch logs when active tab changes
  useEffect(() => {
    let isMounted = true;
    const load = async () => {
      await fetchLogs(activeLogTab);
    };
    load().then(() => {
      if (!isMounted) return;
    });
    return () => {
      isMounted = false;
    };
  }, [activeLogTab, fetchLogs]);

  // Load root folder when selected server or login status changes
  useEffect(() => {
    if (isLoggedIn) {
      const timer = setTimeout(() => {
        fetchFolder('/');
      }, 0);
      return () => clearTimeout(timer);
    }
  }, [isLoggedIn, fetchFolder]);

  // When selectedServerId changes, clear credentials and reset explorer login status
  const handleServerChange = useCallback((serverId) => {
    setSelectedServerId(serverId);
    setUsername('');
    setPassword('');
    setIsLoggedIn(false);
    setFolderData({ '/': [] });
    setExpandedFolders({ '/': true });
    setSelectedPath('/');
    setPreviewFile(null);
    setPreviewData(null);
  }, []);

  const getSelectedItem = useCallback(() => {
    for (const items of Object.values(folderData)) {
      const found = items.find((item) => item.fullName === selectedPath);
      if (found) return found;
    }
    return selectedPath === '/' ? { fullName: '/', isFolder: true } : null;
  }, [folderData, selectedPath]);

  const getUploadTargetPath = useCallback(() => {
    const selectedItem = getSelectedItem();
    if (!selectedItem || selectedItem.isFolder) return selectedPath || '/';
    const parentPath = selectedItem.fullName.substring(0, selectedItem.fullName.lastIndexOf('/')) || '/';
    return parentPath;
  }, [getSelectedItem, selectedPath]);

  // Server management functions
  const handleCreateServer = async (e) => {
    e.preventDefault();
    if (!newServerName || !newServerHost || !newServerPort || !newServerUser || !newServerPass) {
      showToast('Lütfen tüm alanları doldurun.', 'error');
      return;
    }

    const portNum = parseInt(newServerPort, 10);
    if (isNaN(portNum) || portNum <= 0 || portNum > 65535) {
      showToast('Lütfen geçerli bir port girin (1-65535).', 'error');
      return;
    }

    try {
      const newConfig = {
        name: newServerName,
        host: newServerHost.trim(),
        port: portNum,
        username: newServerUser,
        password: newServerPass,
        isActive: true
      };
      await axios.post(`${API_BASE_URL}/servers`, newConfig);
      showToast(`"${newServerName}" FTP sunucusu başarıyla oluşturuldu.`);
      
      setNewServerName('');
      setNewServerHost('127.0.0.1');
      setNewServerPort('');
      setNewServerUser('');
      setNewServerPass('');
      
      await fetchFtpServers();
    } catch (error) {
      showToast(`Sunucu oluşturma hatası: ${error.response?.data || error.message}`, 'error');
    }
  };

  const handleDeleteServer = (id, name) => {
    if (id === 'default') {
      showToast('Varsayılan FTP sunucusu silinemez.', 'error');
      return;
    }

    requestDoubleConfirm(
      "Sunucu Silme Onayı",
      `"${name}" sunucusunu silmek istediğinize emin misiniz? Bu sunucuya ait tüm dosya ve klasörler kalıcı olarak silinecektir!`,
      async () => {
        try {
          await axios.delete(`${API_BASE_URL}/servers/${id}`);
          showToast(`"${name}" sunucusu başarıyla silindi.`);
          await fetchFtpServers();
        } catch (error) {
          showToast(`Sunucu silme hatası: ${error.response?.data || error.message}`, 'error');
        }
      }
    );
  };

  const handleStartServer = async (id, name) => {
    try {
      await axios.post(`${API_BASE_URL}/servers/${id}/start`);
      showToast(`"${name}" sunucusu başlatıldı.`);
      await fetchFtpServers();
    } catch (error) {
      showToast(`Sunucu başlatılamadı: ${error.response?.data || error.message}`, 'error');
    }
  };

  const handleStopServer = async (id, name) => {
    try {
      await axios.post(`${API_BASE_URL}/servers/${id}/stop`);
      showToast(`"${name}" sunucusu durduruldu.`);
      await fetchFtpServers();
    } catch (error) {
      showToast(`Sunucu durdurulamadı: ${error.response?.data || error.message}`, 'error');
    }
  };

  // Handle click on folders
  const handleFolderClick = async (item) => {
    const path = item.fullName;
    setSelectedPath(path);
    
    // Toggle expand state
    const isOpen = !!expandedFolders[path];
    setExpandedFolders(prev => ({ ...prev, [path]: !isOpen }));

    // Load folder contents if opening and not loaded yet
    if (!isOpen && !folderData[path]) {
      await fetchFolder(path);
    }
  };

  // Handle Refresh button
  const handleRefresh = async () => {
    addLog('Dizinler yenileniyor...', 'INFO', false);
    const pathsToRefresh = Object.keys(expandedFolders).filter(p => expandedFolders[p]);
    for (const path of pathsToRefresh) {
      await fetchFolder(path);
    }
    showToast('Dizin yapısı yenilendi.');
  };

  // Folder creation
  const handleCreateFolder = async () => {
    const folderName = prompt('Oluşturmak istediğiniz klasörün adını giriniz:');
    if (!folderName) return;

    const targetDirectory = getUploadTargetPath();
    const parentPath = targetDirectory.endsWith('/') ? targetDirectory : targetDirectory + '/';
    const newFolderPath = parentPath + folderName;

    setLoading(true);
    try {
      await axios.post(`${API_BASE_URL}/create-folder?path=${encodeURIComponent(newFolderPath)}`, null, {
        headers: { 
          'X-FTP-Server-Id': selectedServerId,
          'X-FTP-Username': username,
          'X-FTP-Password': password
        }
      });
      showToast(`"${folderName}" klasörü oluşturuldu.`);
      addLog(`Klasör oluşturuldu: "${newFolderPath}"`, 'INFO', true);
      
      // Refresh parent folder
      await fetchFolder(targetDirectory);
    } catch (error) {
      showToast(`Klasör oluşturma hatası: ${error.response?.data?.message || error.message}`, 'error');
      addLog(`Klasör oluşturma hatası: ${error.message}`, 'ERROR', true);
    } finally {
      setLoading(false);
    }
  };

  // Delete File or Folder
  const handleDeleteItem = (item) => {
    requestDoubleConfirm(
      "Öğe Silme Onayı",
      `"${item.name}" öğesini silmek istediğinize emin misiniz?`,
      async () => {
        setLoading(true);
        try {
          await axios.delete(`${API_BASE_URL}/delete?path=${encodeURIComponent(item.fullName)}&isFolder=${item.isFolder}`, {
            headers: { 
              'X-FTP-Server-Id': selectedServerId,
              'X-FTP-Username': username,
              'X-FTP-Password': password
            }
          });
          showToast(`"${item.name}" başarıyla silindi.`);
          addLog(`Öğe silindi: "${item.fullName}"`, 'WARNING', true);

          // Refresh parent folder
          const parentPath = item.fullName.substring(0, item.fullName.lastIndexOf('/')) || '/';
          await fetchFolder(parentPath);
        } catch (error) {
          showToast(`Silme hatası: ${error.response?.data || error.message}`, 'error');
          addLog(`Silme hatası ("${item.fullName}"): ${error.message}`, 'ERROR', true);
        } finally {
          setLoading(false);
        }
      }
    );
  };

  // Rename File or Folder
  const handleRenameItem = async (item, newName) => {
    if (!newName || newName.trim() === '' || newName === item.name) return;

    let finalNewName = newName.trim();
    if (!item.isFolder) {
      const dotIdx = item.name.lastIndexOf('.');
      if (dotIdx !== -1) {
        const originalExt = item.name.substring(dotIdx); // e.g. ".txt"
        if (!finalNewName.toLowerCase().endsWith(originalExt.toLowerCase())) {
          finalNewName = finalNewName + originalExt;
        }
      }
    }

    if (finalNewName === item.name) return;

    const parentPath = item.fullName.substring(0, item.fullName.lastIndexOf('/')) || '/';
    const targetPath = (parentPath.endsWith('/') ? parentPath : parentPath + '/') + finalNewName;

    setLoading(true);
    try {
      await axios.post(`${API_BASE_URL}/rename?sourcePath=${encodeURIComponent(item.fullName)}&targetPath=${encodeURIComponent(targetPath)}`, null, {
        headers: { 
          'X-FTP-Server-Id': selectedServerId,
          'X-FTP-Username': username,
          'X-FTP-Password': password
        }
      });
      showToast(`"${item.name}" öğesi "${finalNewName}" olarak değiştirildi.`);
      addLog(`Öğe adı değiştirildi: "${item.fullName}" -> "${targetPath}"`, 'INFO', true);

      // If selected path was the renamed item, update it
      if (selectedPath === item.fullName) {
        setSelectedPath(targetPath);
      }

      // Refresh parent directory
      await fetchFolder(parentPath);
    } catch (error) {
      showToast(`Ad değiştirme hatası: ${error.response?.data || error.message}`, 'error');
      addLog(`Ad değiştirme hatası ("${item.fullName}"): ${error.message}`, 'ERROR', true);
    } finally {
      setLoading(false);
    }
  };

  // Move File or Folder (Drag and Drop)
  const handleMoveItem = async (sourceItem, targetFolderItem) => {
    if (!sourceItem || !targetFolderItem) return;
    
    // Can't move to itself
    if (sourceItem.fullName === targetFolderItem.fullName) return;

    // Calculate parent path of source item
    const sourceParentPath = sourceItem.fullName.substring(0, sourceItem.fullName.lastIndexOf('/')) || '/';
    
    // Can't move to its current parent
    if (sourceParentPath === targetFolderItem.fullName) return;

    // Prevent moving folder inside itself or its own subfolders
    if (targetFolderItem.fullName.startsWith(sourceItem.fullName + '/')) {
      showToast('Bir klasör kendi altına veya kendi alt klasörlerine taşınamaz.', 'error');
      return;
    }

    const targetPath = (targetFolderItem.fullName.endsWith('/') ? targetFolderItem.fullName : targetFolderItem.fullName + '/') + sourceItem.name;

    setLoading(true);
    try {
      await axios.post(`${API_BASE_URL}/rename?sourcePath=${encodeURIComponent(sourceItem.fullName)}&targetPath=${encodeURIComponent(targetPath)}`, null, {
        headers: { 
          'X-FTP-Server-Id': selectedServerId,
          'X-FTP-Username': username,
          'X-FTP-Password': password
        }
      });
      showToast(`"${sourceItem.name}" başarıyla "${targetFolderItem.name || targetFolderItem.fullName}" klasörüne taşındı.`);
      addLog(`Öğe taşındı: "${sourceItem.fullName}" -> "${targetPath}"`, 'INFO', true);

      // Refresh both source parent directory and destination directory
      await fetchFolder(sourceParentPath);
      await fetchFolder(targetFolderItem.fullName);
    } catch (error) {
      showToast(`Taşıma hatası: ${error.response?.data || error.message}`, 'error');
      addLog(`Taşıma hatası ("${sourceItem.fullName}"): ${error.message}`, 'ERROR', true);
    } finally {
      setLoading(false);
    }
  };

  // Download File
  const handleDownloadFile = async (item) => {
    try {
      addLog(`"${item.name}" dosyası indiriliyor...`, 'INFO', false);
      const response = await axios.get(`${API_BASE_URL}/download?remotePath=${encodeURIComponent(item.fullName)}`, {
        headers: { 
          'X-FTP-Server-Id': selectedServerId,
          'X-FTP-Username': username,
          'X-FTP-Password': password
        },
        responseType: 'blob'
      });
      
      const blob = new Blob([response.data], { type: 'application/octet-stream' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', item.name);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
      
      addLog(`"${item.name}" dosyası indirildi.`, 'INFO', false);
    } catch (error) {
      showToast(`Dosya indirme hatası: ${error.message}`, 'error');
      addLog(`Dosya indirme hatası ("${item.name}"): ${error.message}`, 'ERROR', true);
    }
  };

  // File Upload Handlers
  const handleFileChange = (e) => {
    if (e.target.files && e.target.files.length > 0) {
      setSelectedFile(e.target.files[0]);
      addLog(`Dosya seçildi: "${e.target.files[0].name}"`, 'INFO', false);
    }
  };

  const handleDragOver = (e) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleDrop = (e) => {
    e.preventDefault();
    setIsDragOver(false);
    if (!hasPermission('files.upload')) {
      showToast('Dosya yukleme yetkiniz yok.', 'error');
      return;
    }
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      setSelectedFile(e.dataTransfer.files[0]);
      addLog(`Dosya sürüklendi: "${e.dataTransfer.files[0].name}"`, 'INFO', false);
    }
  };

  const triggerFileInput = () => {
    if (!hasPermission('files.upload')) {
      showToast('Dosya yukleme yetkiniz yok.', 'error');
      return;
    }
    fileInputRef.current?.click();
  };

  const handleUpload = async () => {
    if (!selectedFile) {
      showToast('Lütfen önce yüklenecek bir dosya seçin.', 'error');
      return;
    }

    setLoading(true);

    const CHUNK_UPLOAD_THRESHOLD = 1 * 1024 * 1024; // 1 MB for testing
    const CHUNK_SIZE = 512 * 1024; // 512 KB chunks for testing
    const uploadTargetPath = getUploadTargetPath();

    if (selectedFile.size > CHUNK_UPLOAD_THRESHOLD) {
      // Chunked Upload
      addLog(`"${selectedFile.name}" dosyası parçalı yükleme (chunked) ile gönderiliyor (${(selectedFile.size / (1024 * 1024)).toFixed(2)} MB)...`, 'INFO', false);
      
      const uploadId = 'chunk_' + Date.now() + '_' + Math.random().toString(36).substring(2, 9);
      const totalChunks = Math.ceil(selectedFile.size / CHUNK_SIZE);
      
      setUploadProgress(0);
      setUploadStatus(`Parça 1/${totalChunks} yükleniyor...`);

      try {
        for (let i = 0; i < totalChunks; i++) {
          const start = i * CHUNK_SIZE;
          const end = Math.min(start + CHUNK_SIZE, selectedFile.size);
          const chunk = selectedFile.slice(start, end);
          
          const formData = new FormData();
          formData.append('file', chunk, selectedFile.name);
          formData.append('uploadId', uploadId);
          formData.append('chunkIndex', i.toString());
          formData.append('totalChunks', totalChunks.toString());
          formData.append('fileName', selectedFile.name);
          formData.append('currentPath', uploadTargetPath);

          const progress = Math.round(((i + 1) / totalChunks) * 100);
          setUploadStatus(`Parça ${i + 1}/${totalChunks} yükleniyor...`);
          setUploadProgress(progress);

          await axios.post(`${API_BASE_URL}/upload-chunk`, formData, {
            headers: { 
              'Content-Type': 'multipart/form-data',
              'X-FTP-Server-Id': selectedServerId,
              'X-FTP-Username': username,
              'X-FTP-Password': password
            }
          });
        }

        showToast('Büyük dosya başarıyla birleştirildi ve yüklendi.');
        addLog(`Büyük dosya parçalı yüklendi: "${selectedFile.name}" -> ${uploadTargetPath}`, 'INFO', true);
        setSelectedFile(null);
        await fetchFolder(uploadTargetPath);
      } catch (error) {
        // Cancel upload on backend to clean up chunks
        try {
          await axios.post(`${API_BASE_URL}/cancel-upload?uploadId=${uploadId}`);
        } catch (cancelError) {
          console.error("Geçici dosyalar temizlenirken hata oluştu:", cancelError);
        }
        showToast(`Parçalı yükleme hatası: ${error.response?.data || error.message}`, 'error');
        addLog(`Parçalı yükleme hatası: ${error.message}`, 'ERROR', true);
      } finally {
        setUploadProgress(null);
        setUploadStatus('');
        setLoading(false);
      }

    } else {
      // Regular single file upload
      addLog(`"${selectedFile.name}" dosyası yükleniyor...`, 'INFO', false);
      const formData = new FormData();
      formData.append('file', selectedFile);
      formData.append('currentPath', uploadTargetPath);

      try {
        await axios.post(`${API_BASE_URL}/upload`, formData, {
          headers: { 
            'Content-Type': 'multipart/form-data',
            'X-FTP-Server-Id': selectedServerId,
            'X-FTP-Username': username,
            'X-FTP-Password': password
          }
        });

        showToast('Dosya başarıyla yüklendi.');
        addLog(`Dosya yüklendi: "${selectedFile.name}" -> ${uploadTargetPath}`, 'INFO', true);
        setSelectedFile(null);
        
        // Refresh current folder
        await fetchFolder(uploadTargetPath);
      } catch (error) {
        showToast(`Dosya yükleme hatası: ${error.response?.data || error.message}`, 'error');
        addLog(`Dosya yükleme hatası: ${error.message}`, 'ERROR', true);
      } finally {
        setLoading(false);
      }
    }
  };

  const handleMockLogin = async (e) => {
    e.preventDefault();
    if (!username || !password) {
      showToast('Lütfen kullanıcı adı ve şifre girin.', 'error');
      return;
    }
    setLoading(true);
    try {
      await axios.post(`${API_BASE_URL}/login?username=${encodeURIComponent(username)}`, null, {
        headers: {
          'X-FTP-Server-Id': selectedServerId,
          'X-FTP-Username': username,
          'X-FTP-Password': password
        }
      });
      setIsLoggedIn(true);
      showToast('Bağlantı doğrulandı ve giriş başarılı.');
      fetchLogs(activeLogTab);
    } catch (error) {
      console.error(error);
      showToast(`Giriş başarısız: ${error.response?.data || 'Kullanıcı adı veya şifre hatalı.'}`, 'error');
      setIsLoggedIn(false);
    } finally {
      setLoading(false);
    }
  };

  // Close and clean up preview URL resources
  const handleClosePreview = useCallback(() => {
    if (previewData && previewData.url) {
      URL.revokeObjectURL(previewData.url);
    }
    setPreviewFile(null);
    setPreviewData(null);
  }, [previewData]);

  // Open and download file preview data
  const handlePreviewFile = async (item) => {
    if (previewData && previewData.url) {
      URL.revokeObjectURL(previewData.url);
    }

    const ext = item.name.split('.').pop().toLowerCase();
    const textExtensions = ['txt', 'log', 'json', 'xml', 'js', 'css', 'html', 'csv'];
    const imageExtensions = ['png', 'jpg', 'jpeg', 'gif', 'svg'];
    const pdfExtensions = ['pdf'];

    const isText = textExtensions.includes(ext);
    const isImage = imageExtensions.includes(ext);
    const isPdf = pdfExtensions.includes(ext);

    if (!isText && !isImage && !isPdf) {
      showToast('Bu dosya formatı önizleme için desteklenmiyor.', 'error');
      handleClosePreview();
      return;
    }

    setPreviewFile(item);
    setPreviewLoading(true);
    setPreviewData(null);

    try {
      const response = await axios.get(`${API_BASE_URL}/download?remotePath=${encodeURIComponent(item.fullName)}`, {
        headers: { 
          'X-FTP-Server-Id': selectedServerId,
          'X-FTP-Username': username,
          'X-FTP-Password': password
        },
        responseType: 'blob'
      });

      const blob = response.data;
      if (isImage) {
        const url = URL.createObjectURL(blob);
        setPreviewData({ type: 'image', url });
      } else if (isPdf) {
        const url = URL.createObjectURL(blob);
        setPreviewData({ type: 'pdf', url });
      } else if (ext === 'csv') {
        const text = await blob.text();
        setPreviewData({ type: 'csv', content: text });
      } else {
        const text = await blob.text();
        setPreviewData({ type: 'text', content: text });
      }
    } catch (error) {
      console.error(error);
      showToast('Dosya önizleme verisi alınamadı.', 'error');
      setPreviewFile(null);
      setPreviewData(null);
    } finally {
      setPreviewLoading(false);
    }
  };

  // Revoke preview URL on unmount
  useEffect(() => {
    return () => {
      if (previewData && previewData.url) {
        URL.revokeObjectURL(previewData.url);
      }
    };
  }, [previewData]);

  const toggleAll = () => {
    setExpandedFolders({ '/': true });
    setSelectedPath('/');
    showToast('Dizin ağacı daraltıldı.');
  };

  // Helper to determine file icon
  const getFileIcon = (fileName) => {
    const ext = fileName.split('.').pop().toLowerCase();
    switch (ext) {
      case 'xlsx':
      case 'xls':
        return 'fa-file-excel text-green-600';
      case 'docx':
      case 'doc':
        return 'fa-file-word text-blue-600';
      case 'pdf':
        return 'fa-file-pdf text-red-500';
      case 'png':
      case 'jpg':
      case 'jpeg':
      case 'gif':
        return 'fa-file-image text-purple-500';
      case 'zip':
      case 'rar':
        return 'fa-file-zipper text-yellow-600';
      case 'txt':
      case 'log':
      case 'json':
        return 'fa-file-lines text-gray-500';
      default:
        return 'fa-file text-gray-400';
    }
  };

  // Get search flat view results
  const searchResults = () => {
    const list = [];
    Object.keys(folderData).forEach(path => {
      folderData[path].forEach(item => {
        if (item.name.toLowerCase().includes(searchQuery.toLowerCase())) {
          list.push(item);
        }
      });
    });
    return list;
  };

  // Copy server details to clipboard helper
  const copyServerDetails = (server) => {
    const details = `Host: ${server.host}\nPort: ${server.port}\nUsername: ${server.username}\nPassword: ${server.password}`;
    navigator.clipboard.writeText(details);
    showToast('Bağlantı bilgileri kopyalandı.');
  };

  const resetUserForm = () => {
    setEditingUserId(null);
    setUserForm({ fullName: '', username: '', password: '', roleId: roles[0]?.id || '', isActive: true });
  };

  const resetRoleForm = () => {
    setEditingRoleId(null);
    setRoleForm({ name: '', description: '', permissions: [] });
  };

  const saveUser = async (e) => {
    e.preventDefault();
    try {
      if (editingUserId) {
        await axios.put(`${ACCESS_API_BASE_URL}/users/${editingUserId}`, userForm);
        showToast('Uye guncellendi.');
      } else {
        await axios.post(`${ACCESS_API_BASE_URL}/users`, userForm);
        showToast('Uye eklendi.');
      }
      resetUserForm();
      await fetchAccessData();
    } catch (error) {
      showToast(`Uye kaydedilemedi: ${error.response?.data || error.message}`, 'error');
    }
  };

  const saveRole = async (e) => {
    e.preventDefault();
    try {
      if (editingRoleId) {
        await axios.put(`${ACCESS_API_BASE_URL}/roles/${editingRoleId}`, roleForm);
        showToast('Rol guncellendi.');
      } else {
        await axios.post(`${ACCESS_API_BASE_URL}/roles`, roleForm);
        showToast('Rol eklendi.');
      }
      resetRoleForm();
      await fetchAccessData();
      await refreshCurrentUser();
    } catch (error) {
      showToast(`Rol kaydedilemedi: ${error.response?.data || error.message}`, 'error');
    }
  };

  const deleteUser = async (id) => {
    try {
      await axios.delete(`${ACCESS_API_BASE_URL}/users/${id}`);
      showToast('Uye silindi.');
      await fetchAccessData();
    } catch (error) {
      showToast(`Uye silinemedi: ${error.response?.data || error.message}`, 'error');
    }
  };

  const deleteRole = async (id) => {
    try {
      await axios.delete(`${ACCESS_API_BASE_URL}/roles/${id}`);
      showToast('Rol silindi.');
      await fetchAccessData();
    } catch (error) {
      showToast(`Rol silinemedi: ${error.response?.data || error.message}`, 'error');
    }
  };

  if (!currentUser) {
    return (
      <AccessLogin
        loginForm={loginForm}
        setLoginForm={setLoginForm}
        handleAppLogin={handleAppLogin}
        loading={loading}
        notification={notification}
      />
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gray-50 text-gray-800 font-sans">
      {/* Toast Notification */}
      {notification && (
        <div className={`fixed top-4 right-4 z-50 flex items-center gap-2 px-4 py-3 rounded-lg shadow-lg border text-sm font-medium transition-all ${
          notification.type === 'error' ? 'bg-red-50 text-red-800 border-red-200' : 'bg-green-50 text-green-800 border-green-200'
        }`}>
          <i className={`fa-solid ${notification.type === 'error' ? 'fa-circle-xmark text-red-500' : 'fa-circle-check text-green-500'}`}></i>
          {notification.message}
        </div>
      )}

      {/* Header bar */}
      <Header
        activeView={activeView}
        setActiveView={setActiveView}
        currentUser={currentUser}
        onLogout={handleLogout}
        hasPermission={hasPermission}
      />

      {/* Main Workspace content */}
      <div className="flex-1 flex overflow-hidden">
        {activeView === 'explorer' ? (
          /* ================= EXPLORER VIEW ================= */
          <>
            <Sidebar
              ftpServers={ftpServers}
              selectedServerId={selectedServerId}
              handleServerChange={handleServerChange}
              copyServerDetails={copyServerDetails}
              handleDeleteServer={handleDeleteServer}
              username={username}
              setUsername={setUsername}
              password={password}
              setPassword={setPassword}
              showPassword={showPassword}
              setShowPassword={setShowPassword}
              handleMockLogin={handleMockLogin}
              searchQuery={searchQuery}
              setSearchQuery={setSearchQuery}
              isLoggedIn={isLoggedIn}
              loading={loading}
              handleCreateFolder={handleCreateFolder}
              handleRefresh={handleRefresh}
              toggleAll={toggleAll}
              folderData={folderData}
              expandedFolders={expandedFolders}
              selectedPath={selectedPath}
              setSelectedPath={setSelectedPath}
              handleFolderClick={handleFolderClick}
              handleDeleteItem={handleDeleteItem}
              getFileIcon={getFileIcon}
              searchResultsList={searchResults()}
              handleDownloadFile={handleDownloadFile}
              onFileClick={handlePreviewFile}
              onRenameItem={handleRenameItem}
              onMoveItem={handleMoveItem}
            />

            <UploadPanel
              isLoggedIn={isLoggedIn}
              selectedFile={selectedFile}
              isDragOver={isDragOver}
              handleDragOver={handleDragOver}
              handleDragLeave={handleDragLeave}
              handleDrop={handleDrop}
              triggerFileInput={triggerFileInput}
              fileInputRef={fileInputRef}
              handleFileChange={handleFileChange}
              handleUpload={handleUpload}
              selectedPath={selectedPath}
              getFileIcon={getFileIcon}
              activeLogTab={activeLogTab}
              setActiveLogTab={setActiveLogTab}
              logs={logs}
              expandedLogId={expandedLogId}
              setExpandedLogId={setExpandedLogId}
              uploadProgress={uploadProgress}
              uploadStatus={uploadStatus}
            />

            <PreviewPanel
              file={previewFile}
              previewData={previewData}
              loading={previewLoading}
              onClose={handleClosePreview}
            />
          </>
        ) : activeView === 'servers' ? (
          /* ================= SERVERS VIEW ================= */
          <ServerManager
            newServerName={newServerName}
            setNewServerName={setNewServerName}
            newServerHost={newServerHost}
            setNewServerHost={setNewServerHost}
            newServerPort={newServerPort}
            setNewServerPort={setNewServerPort}
            newServerUser={newServerUser}
            setNewServerUser={setNewServerUser}
            newServerPass={newServerPass}
            setNewServerPass={setNewServerPass}
            handleCreateServer={handleCreateServer}
            ftpServers={ftpServers}
            handleStartServer={handleStartServer}
            handleStopServer={handleStopServer}
            handleDeleteServer={handleDeleteServer}
            copyServerDetails={copyServerDetails}
            canManageServers={hasPermission('servers.manage')}
            canViewCredentials={hasPermission('servers.credentials')}
          />
        ) : (
          <AccessManager
            users={users}
            roles={roles}
            permissions={permissions}
            accessTab={accessTab}
            setAccessTab={setAccessTab}
            userForm={userForm}
            setUserForm={setUserForm}
            roleForm={roleForm}
            setRoleForm={setRoleForm}
            editingUserId={editingUserId}
            editingRoleId={editingRoleId}
            setEditingUserId={setEditingUserId}
            setEditingRoleId={setEditingRoleId}
            saveUser={saveUser}
            saveRole={saveRole}
            deleteUser={deleteUser}
            deleteRole={deleteRole}
            resetUserForm={resetUserForm}
            resetRoleForm={resetRoleForm}
          />
        )}
      </div>

      {/* Custom Double Confirmation Modal */}
      <ConfirmModal confirmModal={confirmModal} setConfirmModal={setConfirmModal} />
    </div>
  );
}

export default App;
