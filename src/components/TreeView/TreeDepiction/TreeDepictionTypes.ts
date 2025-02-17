import { SemanticDomain, SemanticDomainTreeNode } from "api/models";

const MAX_COL_WIDTH = 50; // Max gap.
const MIN_COL_WIDTH = 30; // Multiply this by RATIO_TILE_TO_GAP for min tile width.
export const RATIO_TILE_TO_GAP = 3; // Must be odd.

export function getNumCols(numChildren: number) {
  return numChildren * (RATIO_TILE_TO_GAP + 1) - 1;
}

export function getColWidth(numChildren: number, totalWidth: number) {
  if (!numChildren) {
    return MAX_COL_WIDTH;
  }
  const colWidth = Math.floor(totalWidth / getNumCols(numChildren));
  return Math.min(Math.max(colWidth, MIN_COL_WIDTH), MAX_COL_WIDTH);
}

export enum Direction {
  Down,
  Next,
  Prev,
  Up,
}

export interface TreeDepictionProps {
  animate: (domain: SemanticDomain) => Promise<void>;
  currentDomain: SemanticDomainTreeNode;
}

export interface TreeRowProps extends TreeDepictionProps {
  colWidth: number;
}
