function Header({ activeView, setActiveView, currentUser, onLogout, hasPermission }) {
  return (
    <header className="h-[65px] bg-white border-b border-gray-200 flex items-center px-6 shadow-sm justify-between z-10">
      <div className="flex items-center gap-3">
        <i className="fa-solid fa-cloud-arrow-up text-blue-600 text-3xl"></i>
        <h1 className="font-extrabold text-xl tracking-tight text-gray-900">FTP Dosya Yonetim Paneli</h1>
      </div>

      <div className="flex items-center gap-3">
        <div className="flex gap-2">
          <button
            onClick={() => setActiveView('explorer')}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-lg transition-all ${
              activeView === 'explorer' ? 'bg-blue-600 text-white shadow-md' : 'text-gray-600 hover:bg-gray-100'
            }`}
          >
            <i className="fa-solid fa-folder-tree"></i>
            Dosya Gezgini
          </button>

          {hasPermission('servers.view') && (
            <button
              onClick={() => setActiveView('servers')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-lg transition-all ${
                activeView === 'servers' ? 'bg-blue-600 text-white shadow-md' : 'text-gray-600 hover:bg-gray-100'
              }`}
            >
              <i className="fa-solid fa-server"></i>
              FTP Sunucu Yonetimi
            </button>
          )}

          {hasPermission('access.manage') && (
            <button
              onClick={() => setActiveView('access')}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-lg transition-all ${
                activeView === 'access' ? 'bg-blue-600 text-white shadow-md' : 'text-gray-600 hover:bg-gray-100'
              }`}
            >
              <i className="fa-solid fa-shield-halved"></i>
              Yetki Yonetimi
            </button>
          )}
        </div>

        <div className="flex items-center gap-3 border-l border-gray-200 pl-3">
          <div className="text-right leading-tight">
            <div className="text-xs font-extrabold text-gray-800">{currentUser?.fullName}</div>
            <div className="text-[11px] text-gray-400 font-semibold">{currentUser?.roleName}</div>
          </div>
          <button type="button" onClick={onLogout} className="p-2 rounded-lg text-gray-500 hover:bg-gray-100" title="Cikis">
            <i className="fa-solid fa-right-from-bracket"></i>
          </button>
        </div>
      </div>
    </header>
  );
}

export default Header;
