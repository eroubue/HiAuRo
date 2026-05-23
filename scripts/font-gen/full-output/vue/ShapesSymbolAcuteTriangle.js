import { defineComponent, h } from 'vue';

export const ShapesSymbolAcuteTriangle = defineComponent({
  name: 'ShapesSymbolAcuteTriangle',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M3.75948 3.85814C4.25229 2.58363 4.4987 1.94638 4.91605 1.89598C4.9718 1.88925 5.02816 1.88925 5.0839 1.89598C5.50126 1.94638 5.74766 2.58363 6.24047 3.85814L7.3002 6.59883C7.55689 7.26268 7.68524 7.59461 7.5501 7.83968C7.53135 7.87368 7.50935 7.90578 7.4844 7.93553C7.30455 8.14996 6.94868 8.14996 6.23692 8.14996H3.76303C3.05128 8.14996 2.6954 8.14996 2.51555 7.93553C2.4906 7.90578 2.4686 7.87368 2.44985 7.83968C2.31471 7.59461 2.44306 7.26268 2.69975 6.59882L3.75948 3.85814Z", "fillRule": "evenodd"})
      ]
    );
  }
});
