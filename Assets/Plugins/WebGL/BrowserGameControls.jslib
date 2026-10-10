mergeInto(LibraryManager.library, {
  SetBrowserGameControls: function (state) {
    if (window.setGameControls) window.setGameControls(state);
  }
});
