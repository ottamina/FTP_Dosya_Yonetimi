

function LogViewer({ activeLogTab, setActiveLogTab, logs, expandedLogId, setExpandedLogId, isLoading, error, onRefresh }) {
  return (
    <div className="mt-auto border-t border-gray-200 pt-6">
      {/* Logs Tabs buttons */}
      <div className="flex gap-3 mb-4">
        <button 
          type="button"
          onClick={() => setActiveLogTab('file')}
          data-testid="logs-file-tab"
          className={`flex items-center gap-2 px-5 py-2.5 rounded-lg font-bold text-sm transition shadow-sm ${
            activeLogTab === 'file' 
              ? 'bg-[#0062cc] text-white shadow-md' 
              : 'bg-transparent text-[#495057] hover:bg-gray-100'
          }`}
        >
          <i className="fa-solid fa-file-code"></i>
          Dosya Logları
        </button>
        <button 
          type="button"
          onClick={() => setActiveLogTab('database')}
          data-testid="logs-database-tab"
          className={`flex items-center gap-2 px-5 py-2.5 rounded-lg font-bold text-sm transition shadow-sm ${
            activeLogTab === 'database' 
              ? 'bg-[#0062cc] text-white shadow-md' 
              : 'bg-transparent text-[#495057] hover:bg-gray-100'
          }`}
        >
          <i className="fa-solid fa-database"></i>
          Veritabanı Logları
        </button>
        <button
          type="button"
          onClick={onRefresh}
          disabled={isLoading}
          className="ml-auto rounded-lg border border-gray-200 bg-white px-3 py-2 text-xs font-bold text-gray-700 transition hover:bg-gray-100 disabled:cursor-wait disabled:opacity-60"
          title="Logları yenile"
        >
          <i className={`fa-solid fa-rotate-right mr-1.5 ${isLoading ? 'animate-spin' : ''}`}></i>
          Yenile
        </button>
      </div>

      <h3 className="text-xl font-bold text-gray-800 my-4">
        {activeLogTab === 'file' ? 'JSON Dosya Logları' : 'LiteDB Veritabanı Logları'}
      </h3>

      {/* Log Records Container */}
      <div className="border border-gray-200 rounded-lg bg-white max-h-[360px] overflow-y-auto shadow-sm flex flex-col divide-y divide-gray-100">
        {logs.map((log, index) => {
          // IDs start from 1 in each daily log file, so they repeat across days.
          const logKey = `${log.timestamp}-${log.id}-${log.operation}-${index}`;
          const isExpanded = expandedLogId === logKey;
          
          // Get level classes
          let badgeClass = "bg-blue-50 text-blue-600 border border-blue-200";
          if (log.level === "WARN" || log.level === "WARNING") {
            badgeClass = "bg-yellow-50 text-yellow-600 border border-yellow-200";
          } else if (log.level === "ERROR") {
            badgeClass = "bg-red-50 text-red-600 border border-red-200";
          }

          return (
            <div key={logKey} className="flex flex-col hover:bg-gray-50/20 transition">
              {/* Row click header */}
              <div 
                onClick={() => setExpandedLogId(isExpanded ? null : logKey)}
                className="flex items-center justify-between p-3.5 cursor-pointer select-none"
              >
                <div className="flex items-center gap-3 min-w-0">
                  <span className={`px-2 py-0.5 rounded text-xs font-bold ${badgeClass}`}>
                    {log.level}
                  </span>
                  <span className="text-[11px] text-gray-400 font-medium">
                    {new Date(log.timestamp).toLocaleTimeString()}
                  </span>
                  <span className="text-xs font-bold text-[#495057]">
                    {log.operation}
                  </span>
                  <span className="text-xs text-gray-600 truncate font-medium">
                    {log.message}
                  </span>
                </div>
                <div className="text-gray-400">
                  <i className={`fa-solid ${isExpanded ? 'fa-angle-up' : 'fa-angle-down'}`}></i>
                </div>
              </div>

              {/* Collapsible Details */}
              {isExpanded && (
                <div className="p-4 bg-gray-50 border-t border-gray-100 text-xs font-mono text-[#495057] shadow-inner">
                  <div className="flex justify-between items-center mb-2">
                    <span className="font-bold text-gray-500">Log Detayı (JSON)</span>
                    <span className="text-[10px] text-gray-400 font-semibold">{log.timestamp}</span>
                  </div>
                  <pre className="overflow-x-auto whitespace-pre-wrap leading-relaxed">
                    {JSON.stringify({
                      timestamp: log.timestamp,
                      level: log.level,
                      operation: log.operation,
                      username: log.username || null,
                      roleName: log.roleName || null,
                      message: log.message,
                      exception: log.exception || null
                    }, null, 2)}
                  </pre>
                </div>
              )}
            </div>
          );
        })}

        {isLoading && (
          <div className="p-6 text-center text-sm text-gray-500">
            <i className="fa-solid fa-spinner mr-2 animate-spin"></i>Loglar yükleniyor...
          </div>
        )}

        {!isLoading && error && (
          <div className="p-6 text-center text-sm text-red-600">
            Loglar açılamadı: {error}
          </div>
        )}

        {!isLoading && !error && logs.length === 0 && (
          <div className="text-gray-400 italic p-6 text-center text-sm">
            Herhangi bir log kaydı bulunmuyor.
          </div>
        )}
      </div>
      <p className="mt-2 text-xs text-gray-400">Performans için en yeni 500 kayıt gösterilir.</p>
    </div>
  );
}

export default LogViewer;
