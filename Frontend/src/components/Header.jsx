

function Header({ activeView, setActiveView }) {
  return (
    <header className="h-[65px] bg-white border-b border-gray-200 flex items-center px-6 shadow-sm justify-between z-10">
      <div className="flex items-center gap-3">
        <i className="fa-solid fa-cloud-arrow-up text-blue-600 text-3xl"></i>
        <h1 className="font-extrabold text-xl tracking-tight text-gray-900">FTP Dosya Yönetim Paneli</h1>
      </div>
      
      {/* Navigation Tabs */}
      <div className="flex gap-2">
        <button 
          onClick={() => setActiveView('explorer')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-lg transition-all ${
            activeView === 'explorer' 
              ? 'bg-blue-600 text-white shadow-md' 
              : 'text-gray-600 hover:bg-gray-100'
          }`}
        >
          <i className="fa-solid fa-folder-tree"></i>
          Dosya Gezgini
        </button>
        <button 
          onClick={() => setActiveView('servers')}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-lg transition-all ${
            activeView === 'servers' 
              ? 'bg-blue-600 text-white shadow-md' 
              : 'text-gray-600 hover:bg-gray-100'
          }`}
        >
          <i className="fa-solid fa-server"></i>
          FTP Sunucu Yönetimi
        </button>
      </div>
    </header>
  );
}

export default Header;
