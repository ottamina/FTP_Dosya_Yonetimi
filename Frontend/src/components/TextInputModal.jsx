import { useEffect, useRef } from 'react';

function TextInputModal({ modal, onClose }) {
  const inputRef = useRef(null);

  useEffect(() => {
    if (!modal.isOpen) return;
    requestAnimationFrame(() => {
      inputRef.current?.focus();
      inputRef.current?.select();
    });
  }, [modal.isOpen]);

  if (!modal.isOpen) return null;

  const submit = async (event) => {
    event.preventDefault();
    const normalizedValue = inputRef.current?.value.trim() || '';
    if (!normalizedValue) return;
    onClose();
    await modal.onConfirm?.(normalizedValue);
  };

  return (
    <div
      className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="text-input-modal-title"
      data-testid="text-input-modal"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <form onSubmit={submit} className="bg-white rounded-2xl shadow-2xl border border-gray-100 w-full max-w-md p-6 flex flex-col gap-4">
        <div className="flex items-center gap-3 text-blue-600">
          <i className="fa-solid fa-pen-to-square text-xl"></i>
          <h3 id="text-input-modal-title" className="text-lg font-bold text-gray-900">{modal.title}</h3>
        </div>
        <label className="text-sm font-semibold text-gray-700">
          {modal.label}
          <input
            ref={inputRef}
            defaultValue={modal.initialValue || ''}
            placeholder={modal.placeholder}
            className="mt-2 w-full rounded-lg border border-gray-200 px-3 py-2.5 text-sm focus:outline-none focus:border-blue-500"
            data-testid="text-input-modal-field"
          />
        </label>
        <div className="flex gap-3 justify-end pt-2">
          <button type="button" onClick={onClose} className="rounded-lg bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold px-4 py-2.5 text-sm" data-testid="text-input-modal-cancel">
            İptal
          </button>
          <button type="submit" className="rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-bold px-4 py-2.5 text-sm" data-testid="text-input-modal-confirm">
            {modal.confirmText || 'Kaydet'}
          </button>
        </div>
      </form>
    </div>
  );
}

export default TextInputModal;
