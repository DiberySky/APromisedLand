window.treeGraphPlatform = {
  registerResize(dotNetRef) {
    this.unregisterResize();
    const state = { dotNetRef, timer: null };
    const handler = () => {
      clearTimeout(state.timer);
      state.timer = setTimeout(() => dotNetRef.invokeMethodAsync('OnWindowResize'), 150);
    };
    window.__treeGraphResize = { state, handler };
    window.addEventListener('resize', handler);
  },
  unregisterResize() {
    const current = window.__treeGraphResize;
    if (!current) return;
    clearTimeout(current.state.timer);
    window.removeEventListener('resize', current.handler);
    window.__treeGraphResize = null;
  },
  getViewportWidth() {
    return window.innerWidth;
  }
};
