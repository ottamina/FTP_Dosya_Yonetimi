

function ConfirmModal({ confirmModal, setConfirmModal }) {
  if (!confirmModal.isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4 backdrop-blur-sm transition-all duration-300" role="dialog" aria-modal="true" data-testid="confirm-modal">
      <div className="bg-white rounded-2xl shadow-2xl border border-gray-100 w-full max-w-md overflow-hidden p-6 flex flex-col gap-4 transform scale-100 transition-all">
        
        {/* Header */}
        <div className="flex items-center gap-3 text-red-500">
          <i className="fa-solid fa-triangle-exclamation text-2xl animate-pulse"></i>
          <h3 className="text-lg font-bold text-gray-900">{confirmModal.title}</h3>
        </div>
        
        {/* Message Body */}
        <div className="text-sm text-gray-600 leading-relaxed py-2">
          {confirmModal.step === 1 ? (
            confirmModal.message
          ) : (
            <div className="bg-red-50 border border-red-100 text-red-950 p-4 rounded-xl flex flex-col gap-2 shadow-inner">
              <span className="font-bold text-xs uppercase tracking-wider text-red-600 flex items-center gap-1.5">
                <span className="relative flex h-2 w-2">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-2 w-2 bg-red-500"></span>
                </span>
                
              </span>
              <span>
                Bu işlemi gerçekleştirmek istediğinize <strong>kesinlikle emin misiniz</strong>?
              </span>
            </div>
          )}
        </div>

        {/* Progress indicator */}
        <div className="flex justify-center gap-2 mt-1">
          <span className={`w-2.5 h-2.5 rounded-full transition-all duration-200 ${confirmModal.step === 1 ? 'bg-red-500 w-6' : 'bg-gray-200'}`}></span>
          <span className={`w-2.5 h-2.5 rounded-full transition-all duration-200 ${confirmModal.step === 2 ? 'bg-red-500 w-6' : 'bg-gray-200'}`}></span>
        </div>

        {/* Buttons Row */}
        <div className="flex gap-3.5 mt-4">
          {confirmModal.step === 1 ? (
            <>
              <button 
                type="button"
                onClick={() => setConfirmModal(prev => ({ ...prev, isOpen: false }))}
                className="flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 px-4 rounded-xl text-sm transition-all duration-150 cursor-pointer"
                data-testid="confirm-no"
              >
                Hayır
              </button>
              <button 
                type="button"
                onClick={() => setConfirmModal(prev => ({ ...prev, step: 2 }))}
                className="flex-1 bg-red-500 hover:bg-red-600 text-white font-bold py-2.5 px-4 rounded-xl text-sm transition-all duration-150 shadow-sm hover:shadow-md cursor-pointer"
                data-testid="confirm-yes"
              >
                Evet
              </button>
            </>
          ) : (
            <>
              <button 
                type="button"
                onClick={async () => {
                  setConfirmModal(prev => ({ ...prev, isOpen: false }));
                  if (confirmModal.onConfirm) {
                    await confirmModal.onConfirm();
                  }
                }}
                className="flex-1 bg-red-500 hover:bg-red-600 text-white font-bold py-2.5 px-4 rounded-xl text-sm transition-all duration-150 shadow-sm hover:shadow-md cursor-pointer"
                data-testid="confirm-final-yes"
              >
                Evet
              </button>
              <button 
                type="button"
                onClick={() => setConfirmModal(prev => ({ ...prev, isOpen: false }))}
                className="flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 px-4 rounded-xl text-sm transition-all duration-150 cursor-pointer"
                data-testid="confirm-final-no"
              >
                Hayır
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export default ConfirmModal;
