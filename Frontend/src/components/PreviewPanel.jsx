

function PreviewPanel({ file, previewData, loading, onClose }) {
  if (!file) return null;

  // Format file size helper
  const formatSize = (bytes) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  // CSV parsing helper
  const renderCsvTable = (content) => {
    if (!content) return <div className="text-gray-400 italic text-xs">Veri yok</div>;
    
    const lines = content.split(/\r?\n/).filter(line => line.trim() !== '');
    if (lines.length === 0) return <div className="text-gray-400 italic text-xs">Boş dosya</div>;

    // Detect delimiter (, or ;)
    const firstLine = lines[0];
    const commas = (firstLine.match(/,/g) || []).length;
    const semicolons = (firstLine.match(/;/g) || []).length;
    const delimiter = semicolons > commas ? ';' : ',';

    const rows = lines.map(line => 
      line.split(delimiter).map(cell => {
        let val = cell.trim();
        if (val.startsWith('"') && val.endsWith('"')) {
          val = val.substring(1, val.length - 1);
        }
        return val;
      })
    );

    const headers = rows[0];
    const dataRows = rows.slice(1);

    return (
      <div className="overflow-x-auto border border-gray-200 rounded-xl shadow-inner max-h-[500px]">
        <table className="min-w-full divide-y divide-gray-250 text-[11px] bg-white">
          <thead className="bg-gray-100 font-bold text-gray-700 sticky top-0 border-b border-gray-200">
            <tr>
              {headers.map((h, i) => (
                <th key={i} className="px-3.5 py-2.5 text-left border-r border-gray-200 last:border-r-0">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-150 text-gray-600 font-medium">
            {dataRows.map((row, rowIndex) => (
              <tr key={rowIndex} className="hover:bg-blue-50/20 transition odd:bg-gray-50/30">
                {row.map((cell, cellIndex) => (
                  <td key={cellIndex} className="px-3.5 py-2 border-r border-gray-150 last:border-r-0 max-w-[200px] truncate" title={cell}>
                    {cell}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  };

  return (
    <div className="w-[500px] border-l border-gray-200 bg-white flex flex-col p-6 shadow-2xl relative transition-all duration-300 animate-slide-in" data-testid="preview-panel">
      {/* Header bar */}
      <div className="flex justify-between items-start border-b border-gray-150 pb-4 mb-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span className="font-bold text-sm text-gray-800 truncate">{file.name}</span>
          </div>
          <span className="text-[11px] text-gray-400 font-semibold uppercase tracking-wider block">
            Boyut: {formatSize(file.size || 0)}
          </span>
        </div>
        <button 
          type="button"
          onClick={onClose}
          className="p-1 text-gray-400 hover:text-gray-600 hover:bg-gray-150 rounded transition duration-150 cursor-pointer"
          title="Önizlemeyi Kapat"
          data-testid="preview-close"
        >
          <i className="fa-solid fa-xmark text-lg"></i>
        </button>
      </div>

      {/* Preview Content Area */}
      <div className="flex-1 overflow-y-auto pr-1">
        {loading ? (
          <div className="flex flex-col items-center justify-center h-48 gap-3 text-gray-400 text-xs">
            <i className="fa-solid fa-spinner animate-spin text-2xl text-blue-500"></i>
            Dosya içeriği yükleniyor...
          </div>
        ) : previewData ? (
          <div className="flex flex-col gap-4">
            
            {/* Visual preview conditional layout */}
            {previewData.type === 'image' && (
              <div className="border border-gray-150 rounded-xl p-3 bg-gray-50 flex items-center justify-center shadow-inner">
                <img 
                  src={previewData.url} 
                  alt={file.name} 
                  className="max-w-full max-h-[420px] object-contain rounded-lg shadow-sm border border-white"
                />
              </div>
            )}

            {previewData.type === 'pdf' && (
              <iframe 
                src={previewData.url} 
                className="w-full h-[550px] border border-gray-200 rounded-xl shadow-md"
                title="PDF Önizleme"
              />
            )}

            {previewData.type === 'csv' && (
              <div className="flex flex-col gap-2">
                <div className="text-[10px] text-gray-450 font-bold uppercase tracking-wider">Tablo Görünümü (CSV)</div>
                {renderCsvTable(previewData.content)}
              </div>
            )}

            {previewData.type === 'text' && (
              <div className="flex flex-col gap-2">
                <div className="text-[10px] text-gray-450 font-bold uppercase tracking-wider">Dosya İçeriği</div>
                <pre className="bg-gray-50 p-4 rounded-xl border border-gray-150 font-mono text-[11px] leading-relaxed overflow-auto max-h-[500px] text-gray-700 select-text shadow-inner" data-testid="preview-text">
                  {previewData.content}
                </pre>
              </div>
            )}

          </div>
        ) : (
          <div className="flex flex-col items-center justify-center h-48 text-gray-400 text-xs italic">
            Önizleme yüklenemedi.
          </div>
        )}
      </div>
    </div>
  );
}

export default PreviewPanel;
