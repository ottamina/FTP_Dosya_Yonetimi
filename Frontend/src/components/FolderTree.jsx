

function FolderTree({ 
  folderData, 
  expandedFolders, 
  selectedPath, 
  setSelectedPath, 
  handleFolderClick, 
  handleDeleteItem,
  getFileIcon,
  handleDownloadFile,
  onFileClick,
  onRequestRename,
  onMoveItem
}) {
  
  // Drag and drop event handlers
  const handleDragStart = (e, item) => {
    e.dataTransfer.setData('text/plain', JSON.stringify(item));
  };

  const handleDragOver = (e) => {
    e.preventDefault();
    e.currentTarget.classList.add('bg-blue-100/70', 'border-blue-300');
  };

  const handleDragLeave = (e) => {
    e.currentTarget.classList.remove('bg-blue-100/70', 'border-blue-300');
  };

  const handleDrop = (e, targetFolderItem) => {
    e.preventDefault();
    e.currentTarget.classList.remove('bg-blue-100/70', 'border-blue-300');
    try {
      const rawData = e.dataTransfer.getData('text/plain');
      if (!rawData) return;
      const sourceItem = JSON.parse(rawData);
      if (onMoveItem) {
        onMoveItem(sourceItem, targetFolderItem);
      }
    } catch (err) {
      console.error('Sürükleme verisi ayrıştırılamadı:', err);
    }
  };

  const renderTree = (path) => {
    const items = folderData[path] || [];
    
    return (
      <div className="folder-tree pl-4 ml-1.5 border-l border-gray-200">
        {items.map((item) => {
          if (item.isFolder) {
            const isOpen = !!expandedFolders[item.fullName];
            return (
              <div key={item.fullName} className="my-1.5">
                <div 
                  draggable="true"
                  onDragStart={(e) => handleDragStart(e, item)}
                  onDragOver={handleDragOver}
                  onDragLeave={handleDragLeave}
                  onDrop={(e) => handleDrop(e, item)}
                  className={`explorer-tree-item flex items-center justify-between p-1.5 border border-transparent rounded hover:bg-gray-100 cursor-pointer select-none group transition-all duration-200 ${
                    selectedPath === item.fullName ? 'explorer-tree-item--selected bg-blue-50 text-blue-700 font-semibold' : ''
                  }`}
                  onClick={() => handleFolderClick(item)}
                  data-testid={`tree-item-${item.fullName}`}
                >
                  <span className="flex items-center gap-2 overflow-hidden pointer-events-none">
                    <i className={`fa-solid ${isOpen ? 'fa-folder-open text-yellow-500' : 'fa-folder text-yellow-500'} text-base flex-shrink-0`}></i>
                    <span className="text-[13px] truncate">{item.name}</span>
                  </span>
                  <div className="flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button 
                      type="button"
                      onClick={(e) => { 
                        e.stopPropagation(); 
                        onRequestRename(item);
                      }}
                      className="explorer-tree-action explorer-tree-action--edit text-blue-500 hover:text-blue-700 p-0.5 hover:bg-white rounded shadow-sm"
                      title="Yeniden Adlandır"
                      data-testid={`rename-item-${item.fullName}`}
                    >
                      <i className="fa-solid fa-pen text-[11px] px-1"></i>
                    </button>
                    <button 
                      type="button"
                      onClick={(e) => { e.stopPropagation(); handleDeleteItem(item); }}
                      className="explorer-tree-action explorer-tree-action--delete text-red-500 hover:text-red-700 p-0.5 hover:bg-white rounded shadow-sm"
                      title="Sil"
                      data-testid={`delete-item-${item.fullName}`}
                    >
                      <i className="fa-solid fa-trash text-[11px] px-1"></i>
                    </button>
                  </div>
                </div>
                {isOpen && renderTree(item.fullName)}
              </div>
            );
          } else {
            return (
              <div 
                key={item.fullName} 
                draggable="true"
                onDragStart={(e) => handleDragStart(e, item)}
                className={`explorer-tree-item flex items-center justify-between p-1.5 rounded hover:bg-gray-100 cursor-pointer select-none group transition ${
                  selectedPath === item.fullName ? 'explorer-tree-item--selected bg-blue-50 text-blue-700 font-semibold' : ''
                }`}
                onClick={() => {
                  setSelectedPath(item.fullName);
                  if (onFileClick) onFileClick(item);
                }}
                data-testid={`tree-item-${item.fullName}`}
              >
                <span className="flex items-center gap-2 overflow-hidden pointer-events-none">
                  <i className={`fa-solid ${getFileIcon(item.name)} text-base flex-shrink-0`}></i>
                  <span className="text-[13px] text-gray-700 truncate">{item.name}</span>
                </span>
                <div className="flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button 
                    type="button"
                    onClick={(e) => { e.stopPropagation(); handleDownloadFile(item); }}
                    className="explorer-tree-action explorer-tree-action--download text-green-600 hover:text-green-800 p-0.5 hover:bg-white rounded shadow-sm"
                    title="İndir"
                    data-testid={`download-item-${item.fullName}`}
                  >
                    <i className="fa-solid fa-download text-[11px] px-1"></i>
                  </button>
                  <button 
                    type="button"
                    onClick={(e) => { 
                      e.stopPropagation(); 
                      onRequestRename(item);
                    }}
                    className="explorer-tree-action explorer-tree-action--edit text-blue-500 hover:text-blue-700 p-0.5 hover:bg-white rounded shadow-sm"
                    title="Yeniden Adlandır"
                    data-testid={`rename-item-${item.fullName}`}
                  >
                    <i className="fa-solid fa-pen text-[11px] px-1"></i>
                  </button>
                  <button 
                    type="button"
                    onClick={(e) => { e.stopPropagation(); handleDeleteItem(item); }}
                    className="explorer-tree-action explorer-tree-action--delete text-red-500 hover:text-red-700 p-0.5 hover:bg-white rounded shadow-sm"
                    title="Sil"
                    data-testid={`delete-item-${item.fullName}`}
                  >
                    <i className="fa-solid fa-trash text-[11px] px-1"></i>
                  </button>
                </div>
              </div>
            );
          }
        })}
      </div>
    );
  };

  return (
    <div className="folder-tree">
      <div 
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={(e) => handleDrop(e, { fullName: '/', name: 'Kök Dizin' })}
        className={`explorer-tree-item flex items-center justify-between p-2 rounded hover:bg-gray-100 cursor-pointer font-bold select-none text-[13px] mb-1 transition-all duration-200 border border-transparent ${
          selectedPath === '/' ? 'explorer-tree-item--selected bg-blue-50 text-blue-700' : 'text-gray-800'
        }`}
        onClick={() => setSelectedPath('/')}
        data-testid="tree-root"
      >
        <span className="flex items-center gap-2 pointer-events-none">
          <i className="fa-solid fa-folder-open text-yellow-500"></i>
          Kök Dizin (/)
        </span>
      </div>
      {renderTree('/')}
    </div>
  );
}

export default FolderTree;
