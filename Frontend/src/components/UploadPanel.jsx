
import LogViewer from './LogViewer';

function UploadPanel({
  isLoggedIn,
  selectedFile,
  isDragOver,
  handleDragOver,
  handleDragLeave,
  handleDrop,
  triggerFileInput,
  fileInputRef,
  handleFileChange,
  handleUpload,
  // eslint-disable-next-line no-unused-vars
  selectedPath,
  getFileIcon,
  activeLogTab,
  setActiveLogTab,
  logs,
  expandedLogId,
  setExpandedLogId,
  uploadProgress,
  uploadStatus
}) {
  if (!isLoggedIn) {
    return (
      <main className="flex-1 bg-white p-8 flex flex-col overflow-y-auto">
        <div className="flex-1 flex flex-col items-center justify-center text-center p-8">
          <div className="w-16 h-16 bg-blue-50 text-blue-500 rounded-full flex items-center justify-center text-2xl mb-4 shadow-sm">
            <i className="fa-solid fa-arrow-right-to-bracket"></i>
          </div>
          <h3 className="text-xl font-bold text-gray-800">Bağlantı Kurulmadı</h3>
          <p className="text-sm text-gray-500 max-w-md mt-2 leading-relaxed">
            Dosya yükleme paneli ve sistem loglarına erişmek için lütfen sol taraftaki panelden kullanıcı bilgilerinizle giriş doğrulamasını tamamlayın.
          </p>
        </div>
      </main>
    );
  }

  return (
    <main className="flex-1 bg-white p-8 flex flex-col overflow-y-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">Dosya Yükleme</h2>

      {/* Drag & Drop Area */}
      <div 
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={triggerFileInput}
        className={`w-full max-w-2xl h-44 border-2 border-dashed rounded-lg flex flex-col items-center justify-center cursor-pointer transition-all duration-200 mb-6 p-4 text-center ${
          isDragOver 
            ? 'border-blue-500 bg-blue-50 shadow-inner' 
            : selectedFile 
              ? 'border-green-400 bg-green-50/30' 
              : 'border-gray-300 hover:border-blue-400 hover:bg-gray-50/50'
        }`}
        data-testid="upload-dropzone"
      >
        <input 
          type="file" 
          ref={fileInputRef}
          onChange={handleFileChange}
          className="hidden" 
          data-testid="upload-file-input"
        />
        {selectedFile ? (
          <div className="flex flex-col items-center gap-2">
            <i className={`fa-solid ${getFileIcon(selectedFile.name)} text-4xl`}></i>
            <div className="font-semibold text-gray-800 text-sm truncate max-w-md">{selectedFile.name}</div>
            <div className="text-xs text-gray-400">{(selectedFile.size / 1024).toFixed(2)} KB - Yüklenmeye Hazır</div>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-2 text-gray-400">
            <i className="fa-solid fa-cloud-arrow-up text-4xl text-gray-300 mb-1"></i>
            <span className="text-sm font-semibold text-gray-600">Dosyayı buraya sürükle ya da tıklayıp seç</span>
          </div>
        )}
      </div>

      {/* Upload Button & Progress Row */}
      <div className="flex items-center gap-4 mb-8">
        <button 
          type="button"
          onClick={handleUpload}
          className="bg-blue-600 hover:bg-blue-700 text-white font-bold px-6 py-2 rounded-lg transition shadow-sm hover:shadow-md cursor-pointer flex-shrink-0"
          data-testid="upload-submit"
        >
          FTP'ye Gönder
        </button>

        {uploadProgress !== null && (
          <div className="flex-1 max-w-md flex flex-col gap-1.5 ml-2">
            <div className="flex justify-between text-xs font-semibold text-blue-600">
              <span className="font-bold flex items-center gap-1.5">
                <i className="fa-solid fa-spinner animate-spin text-blue-500"></i>
                {uploadStatus}
              </span>
              <span className="font-bold">%{uploadProgress}</span>
            </div>
            <div className="w-full bg-gray-100 border border-gray-200/50 rounded-full h-2.5 shadow-inner overflow-hidden relative">
              <div 
                className="bg-blue-600 h-full rounded-full transition-all duration-300 ease-out" 
                style={{ width: `${uploadProgress}%` }}
              ></div>
            </div>
          </div>
        )}
      </div>
{/*
       Target directory warning helper
      {selectedPath !== '/' && (
        <div className="mb-6 p-3 bg-blue-50 rounded-lg max-w-2xl text-xs text-blue-800 border border-blue-100 flex items-center gap-2">
          <i className="fa-solid fa-circle-info"></i>
          <span>Dosyalar seçtiğiniz <strong>{selectedPath}</strong> klasörünün içerisine yüklenecektir.</span>
        </div>
      )}
*/}

      {/* Logs Section */}
      <LogViewer
        activeLogTab={activeLogTab}
        setActiveLogTab={setActiveLogTab}
        logs={logs}
        expandedLogId={expandedLogId}
        setExpandedLogId={setExpandedLogId}
      />
    </main>
  );
}

export default UploadPanel;
