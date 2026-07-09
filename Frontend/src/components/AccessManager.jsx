const permissionLabels = {
  'files.view': 'Dosyalari goruntule',
  'files.upload': 'Dosya yukle',
  'files.download': 'Dosya indir',
  'files.modify': 'Dosya/klasor degistir ve sil',
  'servers.view': 'FTP sunucularini gor',
  'servers.manage': 'FTP sunucusu kur, baslat, durdur, sil',
  'servers.credentials': 'FTP kullanici adi ve sifrelerini gor',
  'logs.view': 'Loglari gor',
  'access.manage': 'Uyeleri, rolleri ve erisimleri yonet'
};

function AccessManager({
  users,
  roles,
  permissions,
  accessTab,
  setAccessTab,
  userForm,
  setUserForm,
  roleForm,
  setRoleForm,
  editingUserId,
  editingRoleId,
  setEditingUserId,
  setEditingRoleId,
  saveUser,
  saveRole,
  deleteUser,
  deleteRole,
  resetUserForm,
  resetRoleForm
}) {
  const togglePermission = (permission) => {
    setRoleForm((prev) => ({
      ...prev,
      permissions: prev.permissions.includes(permission)
        ? prev.permissions.filter((item) => item !== permission)
        : [...prev.permissions, permission]
    }));
  };

  return (
    <main className="flex-1 bg-gray-50 p-8 overflow-y-auto">
      <div className="max-w-7xl mx-auto flex flex-col gap-6">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-3xl font-extrabold text-gray-900">Yetki Yonetimi</h2>
            
          </div>
          <div className="flex gap-2 bg-white border border-gray-200 rounded-lg p-1">
            {[
              ['users', 'Uyeler', 'fa-users'],
              ['roles', 'Roller', 'fa-id-badge'],
              ['access', 'Erisim', 'fa-key']
            ].map(([key, label, icon]) => (
              <button
                key={key}
                type="button"
                onClick={() => setAccessTab(key)}
                className={`px-4 py-2 rounded-md text-sm font-bold flex items-center gap-2 ${accessTab === key ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'}`}
              >
                <i className={`fa-solid ${icon}`}></i>
                {label}
              </button>
            ))}
          </div>
        </div>

        {accessTab === 'users' && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <form onSubmit={saveUser} className="bg-white border border-gray-200 rounded-lg p-5 shadow-sm flex flex-col gap-4 h-fit">
              <h3 className="text-lg font-bold text-gray-800">{editingUserId ? 'Uyeyi Guncelle' : 'Yeni Uye'}</h3>
              <input placeholder="Ad Soyad" value={userForm.fullName} onChange={(e) => setUserForm((p) => ({ ...p, fullName: e.target.value }))} className="border border-gray-200 rounded px-3 py-2 text-sm" />
              <input placeholder="Giris kullanici adi" value={userForm.username} onChange={(e) => setUserForm((p) => ({ ...p, username: e.target.value }))} className="border border-gray-200 rounded px-3 py-2 text-sm" />
              <input type="password" placeholder={editingUserId ? 'Sifreyi degistirmek icin doldurun' : 'Giris sifresi'} value={userForm.password} onChange={(e) => setUserForm((p) => ({ ...p, password: e.target.value }))} className="border border-gray-200 rounded px-3 py-2 text-sm" />
              <select value={userForm.roleId} onChange={(e) => setUserForm((p) => ({ ...p, roleId: e.target.value }))} className="border border-gray-200 rounded px-3 py-2 text-sm">
                <option value="">Rol secin</option>
                {roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
              </select>
              <label className="flex items-center gap-2 text-sm font-semibold text-gray-700">
                <input type="checkbox" checked={userForm.isActive} onChange={(e) => setUserForm((p) => ({ ...p, isActive: e.target.checked }))} />
                Aktif kullanici
              </label>
              <div className="flex gap-2">
                <button type="submit" className="flex-1 bg-blue-600 text-white rounded py-2 text-sm font-bold">{editingUserId ? 'Guncelle' : 'Ekle'}</button>
                {editingUserId && <button type="button" onClick={resetUserForm} className="px-4 bg-gray-100 text-gray-700 rounded text-sm font-bold">Vazgec</button>}
              </div>
            </form>

            <div className="lg:col-span-2 bg-white border border-gray-200 rounded-lg shadow-sm overflow-hidden">
              {users.map((user) => (
                <div key={user.id} className="flex items-center justify-between p-4 border-b border-gray-100 last:border-b-0">
                  <div>
                    <div className="font-bold text-gray-800">{user.fullName}</div>
                    <div className="text-xs text-gray-500">{user.username} - {user.roleName} - {user.isActive ? 'Aktif' : 'Pasif'}</div>
                  </div>
                  <div className="flex gap-2">
                    <button type="button" onClick={() => { setEditingUserId(user.id); setUserForm({ fullName: user.fullName, username: user.username, password: '', roleId: user.roleId, isActive: user.isActive }); }} className="px-3 py-1.5 bg-gray-100 text-gray-700 rounded text-xs font-bold">Duzenle</button>
                    <button type="button" onClick={() => deleteUser(user.id)} disabled={user.id === 'admin'} className="px-3 py-1.5 bg-red-50 text-red-600 rounded text-xs font-bold disabled:text-gray-400 disabled:bg-gray-100">Sil</button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {(accessTab === 'roles' || accessTab === 'access') && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <form onSubmit={saveRole} className="bg-white border border-gray-200 rounded-lg p-5 shadow-sm flex flex-col gap-4 h-fit">
              <h3 className="text-lg font-bold text-gray-800">{editingRoleId ? 'Rolu Guncelle' : 'Yeni Rol'}</h3>
              <input placeholder="Rol adi" value={roleForm.name} onChange={(e) => setRoleForm((p) => ({ ...p, name: e.target.value }))} disabled={editingRoleId === 'admin'} className="border border-gray-200 rounded px-3 py-2 text-sm disabled:bg-gray-50" />
              <textarea placeholder="Aciklama" value={roleForm.description} onChange={(e) => setRoleForm((p) => ({ ...p, description: e.target.value }))} className="border border-gray-200 rounded px-3 py-2 text-sm min-h-20" />
              <div className="grid grid-cols-1 gap-2">
                {permissions.map((permission) => (
                  <label key={permission} className="flex items-start gap-2 text-sm text-gray-700 border border-gray-100 rounded p-2">
                    <input type="checkbox" checked={roleForm.permissions.includes(permission)} disabled={editingRoleId === 'admin'} onChange={() => togglePermission(permission)} className="mt-1" />
                    <span>
                      <strong>{permissionLabels[permission] || permission}</strong>
                      <span className="block text-[11px] text-gray-400">{permission}</span>
                    </span>
                  </label>
                ))}
              </div>
              <div className="flex gap-2">
                <button type="submit" className="flex-1 bg-blue-600 text-white rounded py-2 text-sm font-bold">{editingRoleId ? 'Guncelle' : 'Ekle'}</button>
                {editingRoleId && <button type="button" onClick={resetRoleForm} className="px-4 bg-gray-100 text-gray-700 rounded text-sm font-bold">Vazgec</button>}
              </div>
            </form>

            <div className="lg:col-span-2 grid grid-cols-1 md:grid-cols-2 gap-4">
              {roles.map((role) => (
                <div key={role.id} className="bg-white border border-gray-200 rounded-lg p-4 shadow-sm flex flex-col gap-3">
                  <div className="flex justify-between gap-3">
                    <div>
                      <h4 className="font-extrabold text-gray-800">{role.name}</h4>
                      <p className="text-xs text-gray-500 mt-1">{role.description || 'Aciklama yok'}</p>
                    </div>
                    {role.isSystem && <span className="h-fit text-[10px] bg-blue-50 text-blue-700 border border-blue-100 rounded px-2 py-1 font-bold">Sistem</span>}
                  </div>
                  <div className="flex flex-wrap gap-1">
                    {role.permissions.map((permission) => <span key={permission} className="text-[10px] bg-gray-100 text-gray-600 rounded px-2 py-1 font-semibold">{permissionLabels[permission] || permission}</span>)}
                  </div>
                  <div className="flex gap-2 mt-auto">
                    <button type="button" onClick={() => { setEditingRoleId(role.id); setRoleForm({ name: role.name, description: role.description || '', permissions: role.permissions || [] }); }} className="flex-1 bg-gray-100 text-gray-700 rounded py-1.5 text-xs font-bold">Duzenle</button>
                    <button type="button" onClick={() => deleteRole(role.id)} disabled={role.id === 'admin'} className="flex-1 bg-red-50 text-red-600 rounded py-1.5 text-xs font-bold disabled:text-gray-400 disabled:bg-gray-100">Sil</button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </main>
  );
}

export default AccessManager;
