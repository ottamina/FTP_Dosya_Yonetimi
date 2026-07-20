
import FolderTree from './FolderTree';

function Sidebar({
  ftpServers,
  selectedServerId,
  handleServerChange,
  copyServerDetails,
  handleDeleteServer,
  username,
  setUsername,
  password,
  setPassword,
  showPassword,
  setShowPassword,
  requiresCertificate,
  trustedCertificate,
  onTrustedCertificateChange,
  handleMockLogin,
  searchQuery,
  setSearchQuery,
  isLoggedIn,
  loading,
  handleCreateFolder,
  handleRefresh,
  toggleAll,
  folderData,
  expandedFolders,
  selectedPath,
  setSelectedPath,
  handleFolderClick,
  handleDeleteItem,
  getFileIcon,
  searchResultsList,
  handleDownloadFile,
  onFileClick,
  onRequestRename,
  onMoveItem
}) {
  return (
    <aside className="file-explorer w-[360px] border-r border-gray-200 bg-white flex flex-col p-5 overflow-y-auto">
      {/* Section Header */}
      <div className="flex justify-between items-center mb-3">
        <h2 className="text-lg font-bold text-gray-800">Dosya Yapısı</h2>
        <div className="flex gap-1">
          <button 
            type="button"
            onClick={handleCreateFolder} 
            disabled={!isLoggedIn}
            className={`p-1.5 rounded transition duration-150 ${!isLoggedIn ? 'text-gray-300 cursor-not-allowed' : 'text-gray-500 hover:text-blue-600 hover:bg-gray-100'}`}
            title="Klasör Oluştur"
            data-testid="explorer-create-folder"
          >
            <i className="fa-solid fa-folder-plus text-base"></i>
          </button>
          <button 
            type="button"
            onClick={handleRefresh} 
            disabled={!isLoggedIn}
            className={`p-1.5 rounded transition duration-150 ${!isLoggedIn ? 'text-gray-300 cursor-not-allowed' : 'text-gray-500 hover:text-blue-600 hover:bg-gray-100'}`}
            title="Yenile"
            data-testid="explorer-refresh"
          >
            <i className="fa-solid fa-arrows-rotate text-base"></i>
          </button>
          <button 
            type="button"
            onClick={toggleAll} 
            disabled={!isLoggedIn}
            className={`p-1.5 rounded transition duration-150 ${!isLoggedIn ? 'text-gray-300 cursor-not-allowed' : 'text-gray-500 hover:text-blue-600 hover:bg-gray-100'}`}
            title="Ağacı Daralt"
            data-testid="explorer-collapse"
          >
            <i className="fa-solid fa-chevron-down text-base"></i>
          </button>
        </div>
      </div>

      {/* Server dropdown select */}
      <div className="flex items-center gap-2 mb-3">
        <div className="relative flex-1">
          <select 
            value={selectedServerId}
            onChange={(e) => handleServerChange(e.target.value)}
            className="w-full bg-gray-50 border border-gray-200 rounded px-3 py-2 text-sm focus:outline-none focus:border-blue-500 appearance-none font-semibold text-gray-700"
            data-testid="ftp-server-select"
          >
            {ftpServers.map((server) => (
              <option key={server.id} value={server.id}>
                {server.name} ({server.host}:{server.port})
              </option>
            ))}
          </select>
          <div className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-gray-400">
            <i className="fa-solid fa-chevron-down text-xs"></i>
          </div>
        </div>
        <button 
          type="button"
          onClick={() => {
            const selected = ftpServers.find(s => s.id === selectedServerId);
            if (selected) copyServerDetails(selected);
          }}
          className="p-2 border border-gray-200 rounded hover:bg-gray-50 text-gray-600 transition"
          title="Bilgileri Kopyala"
        >
          <i className="fa-regular fa-copy"></i>
        </button>
        <button 
          type="button"
          onClick={() => {
            const selected = ftpServers.find(s => s.id === selectedServerId);
            if (selected) handleDeleteServer(selected.id, selected.name);
          }}
          disabled={selectedServerId === 'default'}
          className={`p-2 border rounded transition ${
            selectedServerId === 'default'
              ? 'border-gray-100 bg-gray-50 text-gray-400 cursor-not-allowed'
              : 'border-red-100 bg-red-50 text-red-600 hover:bg-red-100'
          }`}
          title="Sunucuyu Sil"
        >
          <i className="fa-solid fa-trash-can"></i>
        </button>
      </div>

      {/* Credentials Inputs row */}
      <form onSubmit={handleMockLogin} className="flex flex-wrap gap-2 items-center mb-4">
        <div className="relative flex-1">
          <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-xs">
            <i className="fa-solid fa-user"></i>
          </span>
          <input 
            type="text" 
            placeholder="Kullanıcı adı"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            className="w-full bg-gray-50 border border-gray-200 rounded pl-7 pr-2 py-1.5 text-xs focus:outline-none focus:border-blue-500 font-semibold"
            data-testid="ftp-username"
          />
        </div>
        <div className="relative flex-1">
          <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-xs">
            <i className="fa-solid fa-lock"></i>
          </span>
          <input 
            type={showPassword ? 'text' : 'password'} 
            placeholder="Şifre"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full bg-gray-50 border border-gray-200 rounded pl-7 pr-7 py-1.5 text-xs focus:outline-none focus:border-blue-500 font-semibold"
            data-testid="ftp-password"
          />
          <button 
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-xs"
            data-testid="ftp-password-toggle"
          >
            <i className={`fa-solid ${showPassword ? 'fa-eye-slash' : 'fa-eye'}`}></i>
          </button>
        </div>
        <button 
          type="submit"
          className="bg-green-600 hover:bg-green-700 text-white rounded p-1.5 transition flex items-center justify-center font-bold"
          title="Giriş Yap / Doğrula"
          data-testid="ftp-login"
        >
          <i className="fa-solid fa-arrow-right-to-bracket text-sm px-1"></i>
        </button>

        {requiresCertificate && (
          <label className="w-full rounded border border-blue-200 bg-blue-50 px-2 py-2 text-[11px] text-blue-800">
            <span className="block font-bold">FTPS sertifikası (.crt)</span>
            <input
              key={selectedServerId}
              type="file"
              accept=".crt"
              required
              onChange={(event) => onTrustedCertificateChange(event.target.files?.[0] || null)}
              className="mt-1 block w-full text-[11px]"
              data-testid="ftp-certificate"
            />
            <span className="mt-1 block text-blue-700">{trustedCertificate ? `Seçildi: ${trustedCertificate.name}` : 'Bağlantı için sunucunun sertifikasını seçin.'}</span>
          </label>
        )}
      </form>

      {/* Search Box */}
      <div className="relative mb-4">
        <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">
          <i className="fa-solid fa-magnifying-glass"></i>
        </span>
        <input 
          type="text" 
          placeholder="Dosya veya klasör ara..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          disabled={!isLoggedIn}
          className="w-full border border-gray-200 rounded-full pl-9 pr-4 py-2 text-sm focus:outline-none focus:border-blue-500 focus:shadow-sm transition disabled:bg-gray-50 disabled:cursor-not-allowed"
          data-testid="explorer-search"
        />
      </div>

      {/* Main Tree view content */}
      <div className="flex-1 overflow-y-auto">
        {!isLoggedIn ? (
          <div className="flex flex-col items-center justify-center py-10 px-4 text-center border border-dashed border-gray-200 rounded-xl bg-gray-50/50 my-2">
            <i className="fa-solid fa-lock text-3xl text-gray-400 mb-3 animate-pulse"></i>
            <h4 className="text-sm font-bold text-gray-700">Bağlantı Kurulmadı</h4>
            <p className="text-xs text-gray-500 leading-relaxed mt-1">
              Klasör ağacını görüntülemek için lütfen kullanıcı adı ve şifrenizi girip sağdaki yeşil butona basarak bağlantı kurun.
            </p>
          </div>
        ) : loading ? (
          <div className="flex justify-center items-center py-4 text-gray-400 text-xs gap-2">
            <i className="fa-solid fa-spinner animate-spin text-sm text-blue-500"></i>
            Yükleniyor...
          </div>
        ) : (
          searchQuery ? (
            // Flat search results view
            <div>
              <div className="text-xs text-gray-400 font-semibold mb-2 px-1">Arama Sonuçları:</div>
              {searchResultsList.length === 0 ? (
                <div className="text-xs text-gray-400 p-2 italic">Sonuç bulunamadı.</div>
              ) : (
                searchResultsList.map(item => (
                  <div 
                    key={item.fullName} 
                    draggable="true"
                    onDragStart={(e) => {
                      e.dataTransfer.setData('text/plain', JSON.stringify(item));
                    }}
                    className={`flex items-center justify-between p-2 rounded hover:bg-gray-100 cursor-pointer group ${selectedPath === item.fullName ? 'bg-blue-50 text-blue-700 font-semibold' : ''}`}
                    onClick={() => {
                      setSelectedPath(item.fullName);
                      if (!item.isFolder && onFileClick) onFileClick(item);
                    }}
                    data-testid={`search-item-${item.fullName}`}
                  >
                    <span className="flex items-center gap-2 overflow-hidden">
                      <i className={`fa-solid ${item.isFolder ? 'fa-folder text-yellow-500' : getFileIcon(item.name)} text-base flex-shrink-0`}></i>
                      <span className="text-[13px] truncate">{item.name}</span>
                    </span>
                    <div className="flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                      {!item.isFolder && (
                        <button 
                          type="button"
                          onClick={(e) => { e.stopPropagation(); handleDownloadFile(item); }}
                          className="text-green-600 hover:text-green-800 p-0.5 hover:bg-white rounded shadow-sm"
                          data-testid={`download-item-${item.fullName}`}
                        >
                          <i className="fa-solid fa-download text-[11px] px-1"></i>
                        </button>
                      )}
                      <button 
                        type="button"
                        onClick={(e) => { 
                          e.stopPropagation(); 
                          onRequestRename(item);
                        }}
                        className="text-blue-500 hover:text-blue-700 p-0.5 hover:bg-white rounded shadow-sm"
                        title="Yeniden Adlandır"
                        data-testid={`rename-item-${item.fullName}`}
                      >
                        <i className="fa-solid fa-pen text-[11px] px-1"></i>
                      </button>
                      <button 
                        type="button"
                        onClick={(e) => { e.stopPropagation(); handleDeleteItem(item); }}
                        className="text-red-500 hover:text-red-700 p-0.5 hover:bg-white rounded shadow-sm"
                        data-testid={`delete-item-${item.fullName}`}
                      >
                        <i className="fa-solid fa-trash text-[11px] px-1"></i>
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          ) : (
            <FolderTree
              folderData={folderData}
              expandedFolders={expandedFolders}
              selectedPath={selectedPath}
              setSelectedPath={setSelectedPath}
              handleFolderClick={handleFolderClick}
              handleDeleteItem={handleDeleteItem}
              getFileIcon={getFileIcon}
              handleDownloadFile={handleDownloadFile}
              onFileClick={onFileClick}
              onRequestRename={onRequestRename}
              onMoveItem={onMoveItem}
            />
          )
        )}
      </div>
    </aside>
  );
}

export default Sidebar;
