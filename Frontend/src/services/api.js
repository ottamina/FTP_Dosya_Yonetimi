import axios from 'axios';

// .NET Core API'mizin çalıştığı temel adres (Backend portuna göre ayarla, örn: 7000 veya 5000)
// Başındaki 'https' yerine 'http', port yerine de '5230' yazıyoruz
const API_BASE_URL = 'http://localhost:5230/api/ftp';

export const ftpApi = {
  // 1. Belirtilen dizindeki dosya ve klasörleri getirir
  getList: async (path) => {
    // URL'deki özel karakterlerin bozulmaması için encodeURIComponent kullanıyoruz
    const response = await axios.get(`${API_BASE_URL}/list?path=${encodeURIComponent(path)}`);
    return response.data;
  },

  // 2. FTP'ye dosya yükler
  uploadFile: async (file, currentPath) => {
    const formData = new FormData();
    formData.append('file', file); // Backend'deki 'IFormFile file' parametresiyle eşleşir
    formData.append('currentPath', currentPath); // Hangi klasörün içine yükleneceği

    const response = await axios.post(`${API_BASE_URL}/upload`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data', // Dosya transferleri için zorunlu header
      },
    });
    return response.data;
  },

  // 3. FTP'den dosya indirir
  downloadFile: async (remotePath) => {
    const response = await axios.get(`${API_BASE_URL}/download?remotePath=${encodeURIComponent(remotePath)}`, {
      responseType: 'blob', // Dosyayı binary veri (Blob) olarak indiriyoruz
    });
    return response.data;
  },

  // 4. Dosya veya klasör siler
  deleteItem: async (path, isFolder) => {
    const response = await axios.delete(`${API_BASE_URL}/delete?path=${encodeURIComponent(path)}&isFolder=${isFolder}`);
    return response.data;
  }
};