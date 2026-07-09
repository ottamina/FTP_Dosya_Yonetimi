import axios from 'axios';

const API_BASE_URL = 'http://localhost:5230/api/ftp';

export const ftpApi = {
  // 1. Belirtilen dizindeki dosya ve klasörleri getirir
  getList: async (path) => {
    const response = await axios.get(`${API_BASE_URL}/list?path=${encodeURIComponent(path)}`);
    return response.data;
  },

  // 2. Standart Küçük Dosya Yükleme (28MB altı garanti yükleme için ideal)
  uploadFile: async (file, currentPath) => {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('currentPath', currentPath);

    const response = await axios.post(`${API_BASE_URL}/upload`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  },

  // 2.5 Chunked (Parçalı) Yükleme Metodu
  // Frontend'de yüklemeyi tetikleyen fonksiyonda: "if (file.size > 28 * 1024 * 1024)" kontrolü koyarak bunu çağırmalısın!
  uploadChunk: async (chunk, uploadId, chunkIndex, totalChunks, fileName, currentPath) => {
    const formData = new FormData();
    formData.append('file', chunk);
    formData.append('uploadId', uploadId);
    formData.append('chunkIndex', chunkIndex);
    formData.append('totalChunks', totalChunks);
    formData.append('fileName', fileName);
    formData.append('currentPath', currentPath);

    const response = await axios.post(`${API_BASE_URL}/upload-chunk`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    });
    return response.data;
  },

  // Yükleme iptal edilirse geçici dizini temizler
  cancelUpload: async (uploadId) => {
    const response = await axios.post(`${API_BASE_URL}/cancel-upload?uploadId=${encodeURIComponent(uploadId)}`);
    return response.data;
  },

  // 3. FTP'den dosya indirir
  downloadFile: async (remotePath) => {
    const response = await axios.get(`${API_BASE_URL}/download?remotePath=${encodeURIComponent(remotePath)}`, {
      responseType: 'blob',
    });
    return response.data;
  },

  // 4. Dosya veya klasör siler
  deleteItem: async (path, isFolder) => {
    const response = await axios.delete(`${API_BASE_URL}/delete?path=${encodeURIComponent(path)}&isFolder=${isFolder}`);
    return response.data;
  },

  // 5. Güvenli Yeniden Adlandırma / Taşıma (Karakter bozulmalarını önler)
  renameItem: async (sourcePath, targetPath) => {
    const response = await axios.post(
      `${API_BASE_URL}/rename?sourcePath=${encodeURIComponent(sourcePath)}&targetPath=${encodeURIComponent(targetPath)}`
    );
    return response.data;
  }
};