import { Fragment, ReactElement, useEffect } from "react";
import { Key } from "ts-key-enum";

import { SemanticDomain, SemanticDomainTreeNode } from "api";

export interface TreeNavigatorProps {
  currentDomain: SemanticDomainTreeNode;
  animate: (domain: SemanticDomain) => Promise<void>;
}

export default function TreeNavigator(props: TreeNavigatorProps): ReactElement {
  const { getNextSibling, getOnlyChild, getParent, getPrevSibling } =
    useTreeNavigation(props);

  // Navigate tree via arrow keys.
  const getArrowKeyDomain = (e: KeyboardEvent): SemanticDomain | undefined => {
    const rtl = document.body.dir === "rtl";
    switch (e.key) {
      case Key.ArrowLeft:
        return rtl ? getNextSibling() : getPrevSibling();
      case Key.ArrowRight:
        return rtl ? getPrevSibling() : getNextSibling();
      case Key.ArrowUp:
        return getParent();
      case Key.ArrowDown:
        return getOnlyChild();
    }
  };

  const navigateDomainArrowKeys = async (e: KeyboardEvent): Promise<void> => {
    const domain = getArrowKeyDomain(e);
    if (domain) {
      await props.animate(domain);
    }
  };

  useEffect(() => {
    window.addEventListener("keydown", navigateDomainArrowKeys);
    return () => window.removeEventListener("keydown", navigateDomainArrowKeys);
  });

  return <Fragment />;
}

interface TreeNavigation {
  getNextSibling: () => SemanticDomain | undefined;
  getOnlyChild: () => SemanticDomain | undefined;
  getParent: () => SemanticDomain | undefined;
  getPrevSibling: () => SemanticDomain | undefined;
}

// Export for unit testing.
export function useTreeNavigation(props: TreeNavigatorProps): TreeNavigation {
  const dom = props.currentDomain;
  return {
    getNextSibling: () => dom.next,
    getOnlyChild: () =>
      dom.children.length === 1 ? dom.children[0] : undefined,
    getParent: () => dom.parent,
    getPrevSibling: () => dom.previous,
  };
}
