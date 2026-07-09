

function ServerManager({
  newServerName,
  setNewServerName,
  newServerHost,
  setNewServerHost,
  newServerPort,
  setNewServerPort,
  newServerUser,
  setNewServerUser,
  newServerPass,
  setNewServerPass,
  handleCreateServer,
  ftpServers,
  handleStartServer,
  handleStopServer,
  handleDeleteServer,
  copyServerDetails,
  canManageServers = false,
  canViewCredentials = false
}) {
  return (
    <main className="flex-1 bg-gray-50 p-8 overflow-y-auto">
      <div className="max-w-6xl mx-auto">
        
        <div className="flex justify-between items-center mb-8">
          <div>
            <h2 className="text-3xl font-extrabold text-gray-900">FTP Sunucu Yönetimi</h2>
            
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Left Column: Create Form */}
          <div className="bg-white border border-gray-200 rounded-xl p-6 shadow-sm flex flex-col gap-6 h-fit">
            <h3 className="text-xl font-bold text-gray-800 flex items-center gap-2 border-b border-gray-100 pb-3">
              <i className="fa-solid fa-folder-plus text-blue-600"></i>
              Yeni FTP Sunucusu Aç
            </h3>
            
            <form onSubmit={handleCreateServer} className="flex flex-col gap-4">
              <div>
                <label className="block text-xs font-bold text-gray-500 uppercase tracking-wider mb-1.5">Sunucu İsmi</label>
                <input 
                  type="text" 
                  placeholder="Örn: Arşiv Deposu, Muhasebe"
                  value={newServerName}
                  onChange={(e) => setNewServerName(e.target.value)}
                  className="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 focus:bg-white font-medium"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-500 uppercase tracking-wider mb-1.5">Sunucu IP / Host</label>
                <input 
                  type="text" 
                  placeholder="Örn: 127.0.0.1 veya ftp.domain.com"
                  value={newServerHost}
                  onChange={(e) => setNewServerHost(e.target.value)}
                  className="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 focus:bg-white font-medium"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-500 uppercase tracking-wider mb-1.5">Port Numarası</label>
                <input 
                  type="number" 
                  placeholder="Örn: 2122, 2123"
                  value={newServerPort}
                  onChange={(e) => setNewServerPort(e.target.value)}
                  className="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 focus:bg-white font-medium"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-500 uppercase tracking-wider mb-1.5">Kullanıcı Adı</label>
                <input 
                  type="text" 
                  placeholder="Örn: ftpuser"
                  value={newServerUser}
                  onChange={(e) => setNewServerUser(e.target.value)}
                  className="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 focus:bg-white font-medium"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-500 uppercase tracking-wider mb-1.5">Şifre</label>
                <input 
                  type="text" 
                  placeholder="Örn: sifre123"
                  value={newServerPass}
                  onChange={(e) => setNewServerPass(e.target.value)}
                  className="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 focus:bg-white font-medium"
                />
              </div>

              <button 
                type="submit"
                disabled={!canManageServers}
                className="mt-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed text-white font-bold py-2.5 rounded-lg shadow transition-all duration-150 flex items-center justify-center gap-2 cursor-pointer"
              >
                <i className="fa-solid fa-play-circle"></i>
                Sunucuyu Oluştur ve Başlat
              </button>
            </form>
          </div>

          {/* Right Column: Server List Cards */}
          <div className="lg:col-span-2 flex flex-col gap-6">
            <h3 className="text-xl font-bold text-gray-800 flex items-center gap-2">
              <i className="fa-solid fa-list-ul text-blue-600"></i>
              Mevcut Sunucular ({ftpServers.length})
            </h3>

            {ftpServers.length === 0 ? (
              <div className="bg-white border border-gray-200 rounded-xl p-8 text-center text-gray-400 italic">
                Tanımlı FTP sunucusu bulunmamaktadır.
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {ftpServers.map((server) => {
                  return (
                    <div 
                      key={server.id} 
                      className="bg-white border border-gray-200 rounded-xl p-5 shadow-sm hover:shadow-md transition flex flex-col justify-between gap-5 relative overflow-hidden"
                    >
                      {/* Top Row: Name and Status */}
                      <div className="flex justify-between items-start">
                        <div>
                          <h4 className="font-extrabold text-gray-800 text-lg">{server.name}</h4>
                          <span className="text-xs text-gray-400 font-semibold">{server.id === 'default' ? 'Varsayılan Sistem Sunucusu' : 'Özel FTP Sunucusu'}</span>
                        </div>
                        
                        <div className="flex items-center gap-1.5">
                          {server.isRunning ? (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold bg-green-50 text-green-700 border border-green-200">
                              <span className="relative flex h-2 w-2">
                                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-75"></span>
                                <span className="relative inline-flex rounded-full h-2 w-2 bg-green-500"></span>
                              </span>
                              Çalışıyor
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold bg-gray-50 text-gray-600 border border-gray-200">
                              <span className="h-2 w-2 rounded-full bg-gray-400"></span>
                              Durdu
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Connection Details Table Info */}
                      <div className="bg-gray-50 rounded-lg p-3.5 text-xs grid grid-cols-2 gap-y-2.5 gap-x-2 border border-gray-100 font-medium">
                        <div>
                          <span className="block text-[10px] text-gray-400 uppercase tracking-wider font-bold">Host</span>
                          <span className="text-gray-700 font-semibold">{server.host}</span>
                        </div>
                        <div>
                          <span className="block text-[10px] text-gray-400 uppercase tracking-wider font-bold">Port </span>
                          <span className="text-blue-600 font-extrabold">{server.port}</span>
                        </div>
                        <div>
                          <span className="block text-[10px] text-gray-400 uppercase tracking-wider font-bold">Kullanıcı Adı</span>
                          <span className="text-gray-700 font-semibold">{canViewCredentials ? server.username : '******'}</span>
                        </div>
                        <div>
                          <span className="block text-[10px] text-gray-400 uppercase tracking-wider font-bold">Şifre</span>
                          <span className="text-gray-700 font-semibold">{canViewCredentials ? server.password : '******'}</span>
                        </div>
                      </div>

                      {/* Action buttons */}
                      <div className="flex gap-2.5 pt-1">
                        <button 
                          type="button"
                          onClick={() => copyServerDetails(server)}
                          disabled={!canViewCredentials}
                          className="flex-1 bg-gray-50 hover:bg-gray-100 border border-gray-200 text-gray-600 text-xs font-bold py-2 rounded-lg transition flex items-center justify-center gap-1.5 cursor-pointer disabled:text-gray-300 disabled:cursor-not-allowed"
                        >
                          <i className="fa-regular fa-copy"></i>
                          Kopyala
                        </button>
                        
                        {server.isRunning ? (
                          <button 
                            type="button"
                            onClick={() => handleStopServer(server.id, server.name)}
                            disabled={!canManageServers}
                            className="flex-1 bg-yellow-50 hover:bg-yellow-100 border border-yellow-200 text-yellow-700 text-xs font-bold py-2 rounded-lg transition flex items-center justify-center gap-1.5 cursor-pointer disabled:text-gray-300 disabled:bg-gray-100 disabled:border-gray-200 disabled:cursor-not-allowed"
                          >
                            <i className="fa-solid fa-stop"></i>
                            Durdur
                          </button>
                        ) : (
                          <button 
                            type="button"
                            onClick={() => handleStartServer(server.id, server.name)}
                            disabled={!canManageServers}
                            className="flex-1 bg-green-50 hover:bg-green-100 border border-green-200 text-green-700 text-xs font-bold py-2 rounded-lg transition flex items-center justify-center gap-1.5 cursor-pointer disabled:text-gray-300 disabled:bg-gray-100 disabled:border-gray-200 disabled:cursor-not-allowed"
                          >
                            <i className="fa-solid fa-play"></i>
                            Başlat
                          </button>
                        )}
                        
                        <button 
                          type="button"
                          onClick={() => handleDeleteServer(server.id, server.name)}
                          disabled={server.id === 'default' || !canManageServers}
                          className={`flex-1 text-xs font-bold py-2 rounded-lg transition flex items-center justify-center gap-1.5 cursor-pointer ${
                            server.id === 'default' || !canManageServers
                              ? 'bg-gray-100 text-gray-400 border border-gray-200 cursor-not-allowed'
                              : 'bg-red-50 hover:bg-red-100 border border-red-200 text-red-600'
                          }`}
                        >
                          <i className="fa-solid fa-trash-can"></i>
                          Sil
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

           

          </div>
        </div>

      </div>
    </main>
  );
}

export default ServerManager;
