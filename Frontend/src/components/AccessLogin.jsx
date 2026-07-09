function AccessLogin({ loginForm, setLoginForm, handleAppLogin, loading, notification }) {
  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center px-6">
      {notification && (
        <div className={`fixed top-4 right-4 z-50 flex items-center gap-2 px-4 py-3 rounded-lg shadow-lg border text-sm font-medium ${
          notification.type === 'error' ? 'bg-red-50 text-red-800 border-red-200' : 'bg-green-50 text-green-800 border-green-200'
        }`}>
          <i className={`fa-solid ${notification.type === 'error' ? 'fa-circle-xmark text-red-500' : 'fa-circle-check text-green-500'}`}></i>
          {notification.message}
        </div>
      )}

      <form onSubmit={handleAppLogin} className="w-full max-w-sm bg-white border border-gray-200 rounded-lg shadow-sm p-6 flex flex-col gap-4">
        <div className="flex items-center gap-3 border-b border-gray-100 pb-4">
          <div className="w-11 h-11 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center text-xl">
            <i className="fa-solid fa-shield-halved"></i>
          </div>
          <div>
            <h1 className="text-lg font-extrabold text-gray-900">FTP Dosya Yonetimi</h1>
            <p className="text-xs text-gray-500 font-medium">Uygulama kullanicisi ile giris yapin.</p>
          </div>
        </div>

        <label className="text-xs font-bold text-gray-500 uppercase tracking-wider">
          Kullanici Adi
          <input
            value={loginForm.username}
            onChange={(e) => setLoginForm((prev) => ({ ...prev, username: e.target.value }))}
            className="mt-1.5 w-full bg-gray-50 border border-gray-200 rounded px-3 py-2 text-sm text-gray-800 normal-case tracking-normal focus:outline-none focus:border-blue-500"
            autoComplete="username"
          />
        </label>

        <label className="text-xs font-bold text-gray-500 uppercase tracking-wider">
          Sifre
          <input
            type="password"
            value={loginForm.password}
            onChange={(e) => setLoginForm((prev) => ({ ...prev, password: e.target.value }))}
            className="mt-1.5 w-full bg-gray-50 border border-gray-200 rounded px-3 py-2 text-sm text-gray-800 normal-case tracking-normal focus:outline-none focus:border-blue-500"
            autoComplete="current-password"
          />
        </label>

        <button
          type="submit"
          disabled={loading}
          className="bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-bold py-2.5 rounded-lg shadow-sm transition flex items-center justify-center gap-2"
        >
          <i className={`fa-solid ${loading ? 'fa-spinner animate-spin' : 'fa-arrow-right-to-bracket'}`}></i>
          Giris Yap
        </button>

        <div className="text-[11px] text-gray-400 border-t border-gray-100 pt-3 leading-relaxed">
          Ilk kurulum kullanicisi: <strong>admin</strong> / <strong>admin123</strong>. Canli kullanimdan once sifreyi degistirin.
        </div>
      </form>
    </div>
  );
}

export default AccessLogin;
